using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace DUTResSystemWebApp.Services
{
    public class EligibilityDecision { public string Recommendation; public string Reason; }
    public class ElectionNotificationDelivery { public int InAppNotifications; public int EmailsSent; public int EmailsFailed; }

    public class ElectionWorkflowService
    {
        public void RunDueWorkflows()
        {
            using (var db = new ResContext())
            {
                foreach (var election in db.ResidenceElections.Where(e => e.Status != "Draft" && e.Status != "Cancelled" && e.Status != "Archived" && e.Status != "Tie Review").ToList())
                    AdvanceElection(db, election, DateTime.Now);
                db.SaveChanges();
            }
        }

        public void AdvanceElection(ResContext db, ResidenceElection election, DateTime now)
        {
            if (election.Status == "Completed")
            {
                if (election.ResultsValidatedAt.HasValue && now >= election.ResultsPublicationAt) PublishResults(db, election);
                return;
            }
            if (election.Status == "Results Published")
            {
                if (election.ResultsPublishedAt.HasValue && now >= election.ResultsPublishedAt.Value.AddDays(30))
                {
                    election.Status = "Archived";
                    election.ArchivedAt = now;
                    Audit(db, election.ResidenceElectionID, "ElectionArchived", "Election data locked for historical reporting.", "System", null);
                }
                return;
            }
            string target = now < election.NominationOpensAt ? "Scheduled" :
                now <= election.NominationClosesAt ? "Applications Open" :
                now < election.CampaignOpensAt ? "Candidate Review" :
                now < election.VotingOpensAt ? "Campaign" :
                now <= election.VotingClosesAt ? "Voting Open" : "Counting";
            // A ballot cannot open until every position has an approved candidate.
            // Once the voting window has closed we must still finish the election;
            // otherwise an incomplete candidate review leaves it stuck forever.
            if (target == "Voting Open" &&
                !HasApprovedCandidateForEveryPosition(db, election.ResidenceElectionID))
                target = "Candidate Review Required";
            if (election.Status == target) return;
            var previous = election.Status;
            election.Status = target;
            Audit(db, election.ResidenceElectionID, "PhaseChanged", previous + " → " + target, "System", null);
            if (target == "Applications Open" || target == "Candidate Review" || target == "Campaign" || target == "Voting Open")
                NotifyResidents(db, election, target);
            if (target == "Counting")
            {
                NotifyResidents(db, election, "Voting Closed");
                CountAndValidate(db, election);
            }
        }

        public bool HasApprovedCandidateForEveryPosition(ResContext db, int electionId)
        {
            var positions = db.ElectionPositions.Where(p => p.ResidenceElectionID == electionId).Select(p => p.ElectionPositionID).ToList();
            return positions.Any() && positions.All(positionId =>
                db.ElectionNominations.Local.Any(n => n.ElectionPositionID == positionId && n.Status == "Approved") ||
                db.ElectionNominations.Any(n => n.ElectionPositionID == positionId && n.Status == "Approved"));
        }

        public EligibilityDecision EvaluateEligibility(ResContext db, ResidenceElection election, int studentId, int? ignoreApplicationId = null)
        {
            var student = db.Students.Find(studentId);
            if (student == null || !student.IsActive) return new EligibilityDecision { Recommendation = "Not Eligible", Reason = "Student registration is inactive." };
            if (student.ResidenceID != election.ResidenceID) return new EligibilityDecision { Recommendation = "Not Eligible", Reason = "Student is not allocated to this residence." };
            if (db.ElectionNominations.Any(n => n.ResidenceElectionID == election.ResidenceElectionID && n.StudentID == studentId && (!ignoreApplicationId.HasValue || n.ElectionNominationID != ignoreApplicationId.Value)))
                return new EligibilityDecision { Recommendation = "Not Eligible", Reason = "Only one House Committee application is permitted per election." };
            if (db.CommitteeAppointments.Any(a => a.StudentID == studentId && a.IsActive))
                return new EligibilityDecision { Recommendation = "Requires Review", Reason = "Student appears to be serving on an active committee." };
            var conduct = db.StudentConductRecords.Where(r => r.StudentID == studentId && r.IsActive).Select(r => r.Severity).ToList();
            if (conduct.Any())
                return new EligibilityDecision { Recommendation = "Not Eligible", Reason = "An unresolved conduct record prevents House Committee candidacy." };
            return new EligibilityDecision { Recommendation = "Eligible", Reason = "Registration and residence allocation verified automatically." };
        }

        public bool CastVote(ResContext db, ResidenceElection election, int positionId, int nominationId, int studentId, out string error)
        {
            error = null;
            if (election.Status != "Voting Open") { error = "Voting is not open."; return false; }
            var student = db.Students.Find(studentId);
            var nomination = db.ElectionNominations.FirstOrDefault(n => n.ElectionNominationID == nominationId && n.ResidenceElectionID == election.ResidenceElectionID && n.ElectionPositionID == positionId && n.Status == "Approved");
            if (student == null || !student.IsActive || student.ResidenceID != election.ResidenceID) { error = "You are not eligible to vote in this residence election."; return false; }
            if (nomination == null) { error = "The selected candidate is not available."; return false; }
            if (election.PreventSelfVote && nomination.StudentID == studentId) { error = "University policy does not permit voting for yourself."; return false; }
            if (db.ElectionParticipations.Any(p => p.ResidenceElectionID == election.ResidenceElectionID && p.ElectionPositionID == positionId && p.StudentID == studentId))
            { Audit(db, election.ResidenceElectionID, "DuplicateVotePrevented", "A duplicate ballot was blocked.", "Student", studentId); error = "You have already voted for this position."; return false; }
            db.ElectionParticipations.Add(new ElectionParticipation { ResidenceElectionID = election.ResidenceElectionID, ElectionPositionID = positionId, StudentID = studentId });
            db.ElectionVotes.Add(new ElectionVote { ResidenceElectionID = election.ResidenceElectionID, ElectionPositionID = positionId, ElectionNominationID = nominationId });
            Audit(db, election.ResidenceElectionID, "VoteSubmitted", "Anonymous ballot accepted for " + positionId + ".", "Student", studentId);
            return true;
        }

        public void CountAndValidate(ResContext db, ResidenceElection election)
        {
            var positions = db.ElectionPositions.Where(p => p.ResidenceElectionID == election.ResidenceElectionID).ToList();
            bool tie = false;
            foreach (var position in positions)
            {
                var ranked = db.ElectionNominations.Where(n => n.ElectionPositionID == position.ElectionPositionID && n.Status == "Approved")
                    .ToList().Select(n => new { Nomination = n, Votes = db.ElectionVotes.Count(v => v.ElectionNominationID == n.ElectionNominationID) })
                    .OrderByDescending(x => x.Votes).ToList();
                foreach (var entry in ranked) { entry.Nomination.VoteCount = entry.Votes; entry.Nomination.IsWinner = false; }
                var cutoff = ranked.Skip(position.Seats - 1).FirstOrDefault();
                if (cutoff != null && ranked.Count(x => x.Votes >= cutoff.Votes) > position.Seats) tie = true;
                else foreach (var entry in ranked.Take(position.Seats)) entry.Nomination.IsWinner = true;
            }
            election.Status = tie ? "Tie Review" : "Completed";
            election.ResultsPublishedAt = null;
            if (!tie)
            {
                // Final validation by the Housing Office is required before the
                // elected roster is made active (UC-E09).
            }
            var voters = db.ElectionParticipations.Where(p => p.ResidenceElectionID == election.ResidenceElectionID).Select(p => p.StudentID).Distinct().Count();
            var eligible = db.Students.Count(s => s.IsActive && s.ResidenceID == election.ResidenceID);
            var turnout = eligible == 0 ? 0 : Math.Round((decimal)voters * 100 / eligible, 1);
            if (election.MinimumTurnoutPercentage.HasValue && turnout < election.MinimumTurnoutPercentage.Value)
                Audit(db, election.ResidenceElectionID, "MinimumTurnoutNotMet", "Turnout was " + turnout + "% (minimum " + election.MinimumTurnoutPercentage.Value + "%).", "System", null);
            Audit(db, election.ResidenceElectionID, tie ? "TieDetected" : "CountingCompleted", tie ? "Tie requires housing office review." : "Winners calculated; awaiting the publication date.", "System", null);
            if (!tie && DateTime.Now >= election.ResultsPublicationAt) PublishResults(db, election);
        }

        public bool ResolveTie(ResContext db, ResidenceElection election, int positionId, int winningNominationId, int staffId, out string error)
        {
            error = null;
            if (election.Status != "Tie Review") { error = "This election is not awaiting tie resolution."; return false; }
            var position = db.ElectionPositions.FirstOrDefault(p => p.ElectionPositionID == positionId && p.ResidenceElectionID == election.ResidenceElectionID);
            var winner = db.ElectionNominations.FirstOrDefault(n => n.ElectionNominationID == winningNominationId && n.ElectionPositionID == positionId && n.ResidenceElectionID == election.ResidenceElectionID && n.Status == "Approved");
            if (position == null || winner == null) { error = "The selected tie resolution is invalid."; return false; }
            foreach (var candidate in db.ElectionNominations.Where(n => n.ElectionPositionID == positionId)) candidate.IsWinner = false;
            winner.IsWinner = true;
            if (!db.CommitteeAppointments.Any(a => a.ResidenceElectionID == election.ResidenceElectionID && a.ElectionPositionID == positionId && a.StudentID == winner.StudentID))
                db.CommitteeAppointments.Add(new CommitteeAppointment { ResidenceElectionID = election.ResidenceElectionID, ElectionPositionID = positionId, StudentID = winner.StudentID });
            var unresolved = db.ElectionPositions.Where(p => p.ResidenceElectionID == election.ResidenceElectionID).Any(p =>
                db.ElectionNominations.Where(n => n.ElectionPositionID == p.ElectionPositionID && n.Status == "Approved").Any() &&
                !db.ElectionNominations.Any(n => n.ElectionPositionID == p.ElectionPositionID && n.IsWinner));
            if (!unresolved)
            {
                election.Status = "Completed";
            }
            Audit(db, election.ResidenceElectionID, "TieResolved", "Tie resolved for " + position.Name + ".", "Staff", staffId);
            return true;
        }

        public bool ConfirmResultsAndActivate(ResContext db, ResidenceElection election, int staffId, out string error)
        {
            error = null;
            if (election.Status != "Completed") { error = "Results can only be validated after counting is complete and all ties are resolved."; return false; }
            if (election.ResultsValidatedAt.HasValue) { error = "These results have already been validated."; return false; }
            if (election.HasUnresolvedDisputes) { error = "Results cannot be validated while an election dispute remains unresolved."; return false; }

            var positions = db.ElectionPositions.Where(p => p.ResidenceElectionID == election.ResidenceElectionID).ToList();
            if (!positions.Any() || positions.Any(p => db.ElectionNominations.Count(n => n.ElectionPositionID == p.ElectionPositionID && n.IsWinner) != p.Seats))
            { error = "Every committee position must have its final winner(s) before validation."; return false; }

            foreach (var winner in db.ElectionNominations.Where(n => n.ResidenceElectionID == election.ResidenceElectionID && n.IsWinner).ToList())
            {
                var eligibility = EvaluateEligibility(db, election, winner.StudentID, winner.ElectionNominationID);
                if (eligibility.Recommendation == "Not Eligible")
                {
                    error = "A winning candidate is no longer eligible: " + eligibility.Reason + " Record the ineligibility and recount before validating results.";
                    return false;
                }
            }

            election.ResultsValidatedAt = DateTime.Now;
            election.ResultsValidatedByStaffID = staffId;
            var activation = ActivateWinners(db, election);
            Audit(db, election.ResidenceElectionID, "ResultsValidated", "Housing Office confirmed final results; all ties and disputes are resolved.", "Staff", staffId);
            Audit(db, election.ResidenceElectionID, "CommitteeActivated", activation + " elected member(s) activated for residence " + election.ResidenceID + " for term " + TermFor(election) + ".", "System", null);
            NotifyWinners(db, election);
            if (DateTime.Now >= election.ResultsPublicationAt) PublishResults(db, election);
            return true;
        }

        public bool RecordWinnerIneligibility(ResContext db, ResidenceElection election, int nominationId, int staffId, string reason, out string error)
        {
            error = null;
            if (election.Status != "Completed" || election.ResultsValidatedAt.HasValue) { error = "A winner can only be disqualified before final results are validated."; return false; }
            var nomination = db.ElectionNominations.FirstOrDefault(n => n.ElectionNominationID == nominationId && n.ResidenceElectionID == election.ResidenceElectionID && n.IsWinner);
            if (nomination == null) { error = "Choose a current winning candidate."; return false; }
            nomination.IsWinner = false;
            nomination.Status = "Disqualified After Voting";
            nomination.ReviewNote = string.IsNullOrWhiteSpace(reason) ? "Found ineligible after voting." : reason.Trim();
            Audit(db, election.ResidenceElectionID, "WinnerDisqualified", "Winner disqualified before activation: " + nomination.ReviewNote, "Staff", staffId);
            election.Status = "Counting";
            CountAndValidate(db, election);
            return true;
        }

        public void Audit(ResContext db, int? electionId, string type, string detail, string actorType, int? actorId)
        { db.ElectionAuditLogs.Add(new ElectionAuditLog { ResidenceElectionID = electionId, EventType = type, Detail = detail, ActorType = actorType, ActorID = actorId }); }

        public void NotifyCandidateApprovalCompleted(ResContext db, ResidenceElection election)
        {
            if (!HasApprovedCandidateForEveryPosition(db, election.ResidenceElectionID) ||
                db.ElectionAuditLogs.Any(a => a.ResidenceElectionID == election.ResidenceElectionID && a.EventType == "CandidateApprovalCompleted"))
                return;
            Audit(db, election.ResidenceElectionID, "CandidateApprovalCompleted", "Every committee position has at least one approved candidate.", "System", null);
            NotifyResidents(db, election, "Candidate Approval Complete");
        }

        public ElectionNotificationDelivery ResendPublishedResultsNotifications(ResContext db, ResidenceElection election, int staffId)
        {
            var delivery = new ElectionNotificationDelivery();
            if (election.Status != "Results Published") return delivery;
            AddDelivery(delivery, NotifyResidents(db, election, "Results Published"));
            AddDelivery(delivery, NotifyWinners(db, election));
            Audit(db, election.ResidenceElectionID, "ResultsNotificationsResent", "Published results were resent by a housing office administrator.", "Staff", staffId);
            return delivery;
        }

        private void PublishResults(ResContext db, ResidenceElection election)
        {
            if (election.Status == "Results Published" || election.Status == "Archived") return;
            if (!election.ResultsValidatedAt.HasValue) return;
            election.Status = "Results Published";
            election.ResultsPublishedAt = DateTime.Now;
            Audit(db, election.ResidenceElectionID, "ResultsPublished", "Results published to the residence.", "System", null);
            NotifyResidents(db, election, "Results Published");
        }

        private ElectionNotificationDelivery NotifyWinners(ResContext db, ResidenceElection election)
        {
            var delivery = new ElectionNotificationDelivery();
            var emailService = new NotificationService();
            foreach (var winner in db.ElectionNominations.Include(n => n.Position).Include(n => n.Student)
                .Where(n => n.ResidenceElectionID == election.ResidenceElectionID && n.IsWinner).ToList())
            {
                var message = "Congratulations. You have been elected as " + winner.Position.Name + " for " + election.Title + ". Your House Committee appointment is now active.";
                db.Notifications.Add(new Notification
                {
                    UserID = winner.StudentID,
                    UserType = "Student",
                    Title = "You have been elected",
                    Message = message,
                    NotificationType = "Election",
                    RelatedID = election.ResidenceElectionID,
                    RelatedType = "ElectionResult",
                    IsRead = false,
                    ExpiryDate = DateTime.Now.AddDays(30)
                });
                delivery.InAppNotifications++;
                if (emailService.SendEmail(winner.Student.Email, "DUT Housing: You have been elected", message)) delivery.EmailsSent++;
                else delivery.EmailsFailed++;
            }
            return delivery;
        }

        private int ActivateWinners(ResContext db, ResidenceElection election)
        {
            var activatedAt = DateTime.Now;
            var previousAppointments = db.CommitteeAppointments
                .Where(a => a.ResidenceID == election.ResidenceID && a.IsActive && a.ResidenceElectionID != election.ResidenceElectionID)
                .ToList();
            foreach (var previous in previousAppointments)
            {
                previous.IsActive = false;
                previous.MemberStatus = "Former House Committee Member";
                previous.EndedAt = activatedAt;
            }
            var count = 0;
            foreach (var winner in db.ElectionNominations
                .Where(n => n.ResidenceElectionID == election.ResidenceElectionID && n.IsWinner)
                .ToList())
            {
                if (!db.CommitteeAppointments.Any(a =>
                    a.ResidenceElectionID == election.ResidenceElectionID &&
                    a.StudentID == winner.StudentID &&
                    a.ElectionPositionID == winner.ElectionPositionID))
                {
                    db.CommitteeAppointments.Add(new CommitteeAppointment
                    {
                        ResidenceElectionID = election.ResidenceElectionID,
                        ElectionPositionID = winner.ElectionPositionID,
                        StudentID = winner.StudentID,
                        ResidenceID = election.ResidenceID,
                        Term = TermFor(election),
                        MemberStatus = "Active House Committee Member",
                        ActivatedAt = activatedAt,
                        AppointedAt = activatedAt
                    });
                    count++;
                }
            }
            if (previousAppointments.Any())
                Audit(db, election.ResidenceElectionID, "PreviousCommitteeDeactivated", previousAppointments.Count + " previous committee appointment(s) ended for residence " + election.ResidenceID + ".", "System", null);
            return count;
        }

        private static string TermFor(ResidenceElection election)
        {
            return election.ResultsPublicationAt.Year + " House Committee term";
        }

        public ElectionNotificationDelivery NotifyResidents(ResContext db, ResidenceElection election, string phase)
        {
            var delivery = new ElectionNotificationDelivery();
            string title = "Residence election update";
            string message = phase == "Applications Open" ? election.Title + ": applications are now open. Apply for a House Committee position." :
                phase == "Candidate Review" ? election.Title + ": applications have closed and are under review." :
                phase == "Candidate Approval Complete" ? election.Title + ": candidate approval is complete. View the campaign profiles." :
                phase == "Campaign" ? election.Title + ": the campaign period has started. View candidate profiles and manifestos." :
                phase == "Voting Open" ? election.Title + ": voting is now open. Submit your anonymous ballot before the closing date." :
                phase == "Voting Closed" ? election.Title + ": voting has closed. Votes are being counted." :
                phase == "Results Published" ? election.Title + ": results have been published. View the elected House Committee." :
                phase == "Cancelled" ? election.Title + ": this election has been cancelled by the housing office." :
                election.Title + ": " + phase + ".";
            var emailService = new NotificationService();
            foreach (var student in db.Students.Where(s => s.IsActive && s.ResidenceID == election.ResidenceID).ToList())
            {
                db.Notifications.Add(new Notification { UserID = student.StudentID, UserType = "Student", Title = title, Message = message, NotificationType = "Election", RelatedID = election.ResidenceElectionID, RelatedType = "Election", IsRead = false, ExpiryDate = DateTime.Now.AddDays(14) });
                delivery.InAppNotifications++;
                if (emailService.SendEmail(student.Email, "DUT Housing: " + title, message)) delivery.EmailsSent++;
                else delivery.EmailsFailed++;
            }
            return delivery;
        }

        private static void AddDelivery(ElectionNotificationDelivery total, ElectionNotificationDelivery addition)
        {
            total.InAppNotifications += addition.InAppNotifications;
            total.EmailsSent += addition.EmailsSent;
            total.EmailsFailed += addition.EmailsFailed;
        }
    }
}
