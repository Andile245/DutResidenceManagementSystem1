using DUTResManagementSystem.Models;
using DUTResManagementSystem.ViewModels;
using DUTResSystemWebApp.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DUTResSystemWebApp.Controllers
{
    public class ElectionController : Controller
    {
        private readonly ResContext db = new ResContext();
        private readonly ElectionWorkflowService workflow = new ElectionWorkflowService();

        public ActionResult Index()
        {
            workflow.RunDueWorkflows();
            var elections = db.ResidenceElections.Include(e => e.Residence).OrderByDescending(e => e.CreatedAt).ToList();
            return View(elections);
        }

        public ActionResult Details(int id)
        {
            workflow.RunDueWorkflows();
            var election = db.ResidenceElections.Include(e => e.Residence).Include(e => e.Positions).FirstOrDefault(e => e.ResidenceElectionID == id);
            if (election == null) return HttpNotFound();
            ViewBag.Candidates = db.ElectionNominations.Include(n => n.Student).Include(n => n.Position).Where(n => n.ResidenceElectionID == id && n.Status == "Approved").ToList();
            ViewBag.CanManage = IsManager();
            var currentStudent = CurrentStudent();
            ViewBag.CanVote = election.Status == "Voting Open" && currentStudent != null && currentStudent.IsActive && currentStudent.ResidenceID == election.ResidenceID;
            ViewBag.VotingUnavailableReason = currentStudent == null
                ? "Sign in as a student to vote."
                : currentStudent.ResidenceID != election.ResidenceID
                    ? "Only students allocated to this residence may vote in this election."
                    : !currentStudent.IsActive
                        ? "Your student registration is inactive, so voting is unavailable."
                        : null;
            var hasApplied = false;
            if (Session["StudentID"] != null)
            {
                var currentStudentId = Convert.ToInt32(Session["StudentID"]);
                hasApplied = db.ElectionNominations.Any(n => n.ResidenceElectionID == id && n.StudentID == currentStudentId);
            }
            ViewBag.HasApplied = hasApplied;
            return View(election);
        }

        public ActionResult Setup()
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            ViewBag.Residences = new SelectList(db.Residences.OrderBy(r => r.Name), "ResidenceID", "Name");
            return View(new ElectionSetupViewModel { NominationOpensAt = DateTime.Now.AddDays(1), NominationClosesAt = DateTime.Now.AddDays(8), CampaignOpensAt = DateTime.Now.AddDays(10), VotingOpensAt = DateTime.Now.AddDays(14), VotingClosesAt = DateTime.Now.AddDays(16), ResultsPublicationAt = DateTime.Now.AddDays(17), Positions = "Chairperson, Deputy Chairperson, Secretary, Treasurer" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Setup(ElectionSetupViewModel model)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            if (model.NominationOpensAt >= model.NominationClosesAt || model.NominationClosesAt > model.CampaignOpensAt || model.CampaignOpensAt > model.VotingOpensAt || model.VotingOpensAt >= model.VotingClosesAt || model.VotingClosesAt > model.ResultsPublicationAt)
                ModelState.AddModelError("", "Dates must follow the application, review, campaign, voting, and results sequence.");
            if (!db.Residences.Any(r => r.ResidenceID == model.ResidenceID))
                ModelState.AddModelError("ResidenceID", "Choose a valid residence.");
            if (db.ResidenceElections.Any(e => e.ResidenceID == model.ResidenceID && e.Status != "Archived" && e.Status != "Cancelled" && model.NominationOpensAt <= e.VotingClosesAt && model.VotingClosesAt >= e.NominationOpensAt))
                ModelState.AddModelError("", "This residence already has an election that overlaps the proposed election period.");
            var positions = (model.Positions ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!positions.Any()) ModelState.AddModelError("Positions", "Add at least one committee position.");
            if (!ModelState.IsValid) { ViewBag.Residences = new SelectList(db.Residences, "ResidenceID", "Name", model.ResidenceID); return View(model); }
            var election = new ResidenceElection { Title = model.Title, ResidenceID = model.ResidenceID, NominationOpensAt = model.NominationOpensAt, NominationClosesAt = model.NominationClosesAt, CampaignOpensAt = model.CampaignOpensAt, VotingOpensAt = model.VotingOpensAt, VotingClosesAt = model.VotingClosesAt, ResultsPublicationAt = model.ResultsPublicationAt, MinimumTurnoutPercentage = model.MinimumTurnoutPercentage, PreventSelfVote = model.PreventSelfVote, Status = "Draft", CreatedByStaffID = (int)Session["StaffID"] };
            db.ResidenceElections.Add(election); db.SaveChanges();
            for (var i = 0; i < positions.Count; i++) db.ElectionPositions.Add(new ElectionPosition { ResidenceElectionID = election.ResidenceElectionID, Name = positions[i], DisplayOrder = i + 1 });
            workflow.Audit(db, election.ResidenceElectionID, "ElectionCreated", "Election configured by housing office.", "Staff", (int)Session["StaffID"]);
            db.SaveChanges(); TempData["Success"] = "Election draft created. Publish it when the schedule and positions are ready.";
            return RedirectToAction("Details", new { id = election.ResidenceElectionID });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Publish(int id)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            var election = db.ResidenceElections.Include(e => e.Positions).FirstOrDefault(e => e.ResidenceElectionID == id);
            if (election == null) return HttpNotFound();
            if (election.Status != "Draft") { TempData["Error"] = "Only draft elections can be published."; return RedirectToAction("Details", new { id }); }
            if (!election.Positions.Any()) { TempData["Error"] = "Add at least one committee position before publishing."; return RedirectToAction("Details", new { id }); }
            election.Status = "Scheduled";
            workflow.Audit(db, id, "ElectionPublished", "Election schedule published by housing office.", "Staff", (int)Session["StaffID"]);
            workflow.AdvanceElection(db, election, DateTime.Now);
            db.SaveChanges();
            TempData["Success"] = "Election published. Students will be notified as phases open.";
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id, string reason)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            var election = db.ResidenceElections.Find(id);
            if (election == null) return HttpNotFound();
            if (election.Status == "Voting Open" || election.Status == "Counting" || election.Status == "Tie Review" || election.Status == "Completed" || election.Status == "Results Published" || election.Status == "Archived") return new HttpStatusCodeResult(400, "This election can no longer be cancelled.");
            election.Status = "Cancelled";
            workflow.Audit(db, id, "ElectionCancelled", string.IsNullOrWhiteSpace(reason) ? "Election cancelled by housing office." : reason.Trim(), "Staff", (int)Session["StaffID"]);
            workflow.NotifyResidents(db, election, "Cancelled");
            db.SaveChanges(); TempData["Success"] = "Election cancelled and affected residents notified.";
            return RedirectToAction("Details", new { id });
        }

        public ActionResult EditSchedule(int id)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            var election = db.ResidenceElections.Find(id); if (election == null) return HttpNotFound();
            if (election.Status == "Results Published" || election.Status == "Tie Review") { TempData["Error"] = "A completed election schedule cannot be changed."; return RedirectToAction("Details", new { id }); }
            return View(new ElectionScheduleViewModel { ElectionId = id, Title = election.Title, NominationOpensAt = election.NominationOpensAt, NominationClosesAt = election.NominationClosesAt, CampaignOpensAt = election.CampaignOpensAt, VotingOpensAt = election.VotingOpensAt, VotingClosesAt = election.VotingClosesAt, ResultsPublicationAt = election.ResultsPublicationAt });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult EditSchedule(ElectionScheduleViewModel model)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            var election = db.ResidenceElections.Find(model.ElectionId); if (election == null) return HttpNotFound();
            if (model.NominationOpensAt >= model.NominationClosesAt || model.NominationClosesAt > model.CampaignOpensAt || model.CampaignOpensAt > model.VotingOpensAt || model.VotingOpensAt >= model.VotingClosesAt || model.VotingClosesAt > model.ResultsPublicationAt)
                ModelState.AddModelError("", "Dates must follow the application, review, campaign, voting, and results sequence.");
            if (!ModelState.IsValid) return View(model);
            election.NominationOpensAt = model.NominationOpensAt; election.NominationClosesAt = model.NominationClosesAt; election.CampaignOpensAt = model.CampaignOpensAt; election.VotingOpensAt = model.VotingOpensAt; election.VotingClosesAt = model.VotingClosesAt; election.ResultsPublicationAt = model.ResultsPublicationAt;
            workflow.AdvanceElection(db, election, DateTime.Now); workflow.Audit(db, election.ResidenceElectionID, "ScheduleChanged", "Election dates updated by housing office.", "Staff", (int)Session["StaffID"]); db.SaveChanges();
            TempData["Success"] = "Schedule updated. The current phase was recalculated automatically."; return RedirectToAction("Details", new { id = election.ResidenceElectionID });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResendResultsNotifications(int id)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            var election = db.ResidenceElections.Find(id);
            if (election == null) return HttpNotFound();
            if (election.Status != "Results Published")
            {
                TempData["Error"] = "Results can only be resent after the election has been published.";
                return RedirectToAction("Details", new { id });
            }
            var delivery = workflow.ResendPublishedResultsNotifications(db, election, (int)Session["StaffID"]);
            db.SaveChanges();
            TempData["Success"] = delivery.InAppNotifications + " in-app notification(s) created and " + delivery.EmailsSent + " email(s) sent.";
            if (delivery.EmailsFailed > 0)
                TempData["Error"] = delivery.EmailsFailed + " email(s) could not be sent. Check the SMTP settings and each student's email address.";
            return RedirectToAction("Details", new { id });
        }

        public ActionResult Apply(int id)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth");
            workflow.RunDueWorkflows();
            var election = db.ResidenceElections.Include(e => e.Positions).FirstOrDefault(e => e.ResidenceElectionID == id);
            if (election == null) return HttpNotFound();
            if (election.Status != "Applications Open") { TempData["Error"] = "Applications are not currently open."; return RedirectToAction("Details", new { id }); }
            if (student.ResidenceID != election.ResidenceID) return new HttpStatusCodeResult(403);
            ViewBag.Election = election; ViewBag.Positions = new SelectList(election.Positions.OrderBy(p => p.DisplayOrder), "ElectionPositionID", "Name");
            return View(new NominationInputViewModel());
        }

        public ActionResult MyApplication(int id)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth");
            var application = db.ElectionNominations.Include(n => n.Election).Include(n => n.Position).FirstOrDefault(n => n.ResidenceElectionID == id && n.StudentID == student.StudentID);
            if (application == null) return RedirectToAction("Apply", new { id });
            return View(application);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReviseApplication(int id, NominationInputViewModel model)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth");
            workflow.RunDueWorkflows();
            var application = db.ElectionNominations.Include(n => n.Position).Include(n => n.Election).FirstOrDefault(n => n.ResidenceElectionID == id && n.StudentID == student.StudentID);
            if (application == null || application.Status != "Pending Candidate Response") return new HttpStatusCodeResult(400, "This application cannot be revised.");
            if (student.ResidenceID != application.Election.ResidenceID) return new HttpStatusCodeResult(403);
            var reviewAllowed = application.Election.Status == "Applications Open" ||
                                application.Election.Status == "Candidate Review" ||
                                application.Election.Status == "Campaign" ||
                                application.Election.Status == "Candidate Review Required";
            if (!reviewAllowed) return new HttpStatusCodeResult(400, "Applications are no longer open for revision.");
            if (!ModelState.IsValid) { application.Manifesto = model.Manifesto; application.Motivation = model.Motivation; return View("MyApplication", application); }
            application.Manifesto = model.Manifesto.Trim(); application.Motivation = model.Motivation.Trim(); application.Status = "Under Review"; application.ReviewNote = null; application.ReviewedAt = null; application.ReviewedByStaffID = null;
            workflow.Audit(db, id, "ApplicationRevised", "Applicant supplied requested information.", "Student", student.StudentID); db.SaveChanges();
            TempData["Success"] = "Your revised application has been returned for review."; return RedirectToAction("MyApplication", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Apply(int id, NominationInputViewModel model, HttpPostedFileBase profilePhoto)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth");
            workflow.RunDueWorkflows();
            var election = db.ResidenceElections.Include(e => e.Positions).FirstOrDefault(e => e.ResidenceElectionID == id);
            if (election == null || election.Status != "Applications Open") return new HttpStatusCodeResult(400, "Applications are closed.");
            if (student.ResidenceID != election.ResidenceID) return new HttpStatusCodeResult(403);
            if (!election.Positions.Any(p => p.ElectionPositionID == model.ElectionPositionID)) ModelState.AddModelError("ElectionPositionID", "Choose a position in this election.");
            if (!ModelState.IsValid) { ViewBag.Election = election; ViewBag.Positions = new SelectList(election.Positions, "ElectionPositionID", "Name"); return View(model); }
            var decision = workflow.EvaluateEligibility(db, election, student.StudentID);
            var nomination = new ElectionNomination { ResidenceElectionID = id, ElectionPositionID = model.ElectionPositionID, StudentID = student.StudentID, Manifesto = model.Manifesto, Motivation = model.Motivation, AcceptedElectionRules = model.AcceptedElectionRules, EligibilityRecommendation = decision.Recommendation, EligibilityReason = decision.Reason, Status = decision.Recommendation == "Not Eligible" ? "Rejected Automatically" : "Submitted" };
            if (profilePhoto != null && profilePhoto.ContentLength > 0 && profilePhoto.ContentLength <= 2 * 1024 * 1024 && new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(profilePhoto.FileName).ToLowerInvariant()))
            { var name = Guid.NewGuid() + Path.GetExtension(profilePhoto.FileName); var folder = Server.MapPath("~/Uploads/ElectionProfiles"); Directory.CreateDirectory(folder); profilePhoto.SaveAs(Path.Combine(folder, name)); nomination.ProfilePhotoPath = "/Uploads/ElectionProfiles/" + name; nomination.CampaignImagePath = nomination.ProfilePhotoPath; }
            db.ElectionNominations.Add(nomination); workflow.Audit(db, id, "HouseCommitteeApplicationSubmitted", "Eligibility recommendation: " + decision.Recommendation, "Student", student.StudentID); db.SaveChanges();
            TempData["Success"] = decision.Recommendation == "Not Eligible" ? decision.Reason : "Your House Committee application has been submitted for review."; return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult WithdrawApplication(int id, string reason)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth");
            workflow.RunDueWorkflows();
            var nomination = db.ElectionNominations.Include(n => n.Election).FirstOrDefault(n => n.ResidenceElectionID == id && n.StudentID == student.StudentID);
            if (nomination == null) return HttpNotFound();
            if (nomination.Election.Status == "Voting Open" || nomination.Election.Status == "Counting" || nomination.Election.Status == "Tie Review" || nomination.Election.Status == "Completed" || nomination.Election.Status == "Results Published" || nomination.Election.Status == "Archived") return new HttpStatusCodeResult(400, "Candidates cannot withdraw after voting opens.");
            nomination.Status = "Withdrawn"; nomination.WithdrawnAt = DateTime.Now; nomination.WithdrawalReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(); nomination.IsWinner = false;
            workflow.Audit(db, id, "CandidateWithdrawn", "Candidate withdrew" + (string.IsNullOrWhiteSpace(reason) ? "." : ": " + reason.Trim()), "Student", student.StudentID);
            db.SaveChanges(); TempData["Success"] = "Your candidacy has been withdrawn.";
            return RedirectToAction("Details", new { id });
        }

        public ActionResult Review(int id)
        {
            if (!IsManager()) return new HttpStatusCodeResult(403); workflow.RunDueWorkflows();
            var election = db.ResidenceElections.Include(e => e.Residence).FirstOrDefault(e => e.ResidenceElectionID == id); if (election == null) return HttpNotFound();
            if (!IsAdmin() && (int?)Session["ResidenceID"] != election.ResidenceID) return new HttpStatusCodeResult(403);
            var applications = db.ElectionNominations.Include(n => n.Student).Include(n => n.Position).Where(n => n.ResidenceElectionID == id).OrderBy(n => n.Status).ToList();
            var applicantIds = applications.Select(n => n.StudentID).Distinct().ToList();
            ViewBag.ConductWarnings = db.StudentConductRecords
                .Where(r => applicantIds.Contains(r.StudentID) && r.IsActive)
                .ToList()
                .GroupBy(r => r.StudentID)
                .ToDictionary(g => g.Key, g => string.Join("; ", g.Select(r => r.Severity + " warning: " + r.Reason)));
            ViewBag.Election = election;
            ViewBag.CanReviewApplications = election.Status == "Applications Open" ||
                                            election.Status == "Candidate Review" ||
                                            election.Status == "Campaign" ||
                                            election.Status == "Candidate Review Required";
            return View(applications);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ReviewNomination(int id, int nominationId, string decision, string note)
        {
            if (!IsManager()) return new HttpStatusCodeResult(403); var nomination = db.ElectionNominations.Include(n => n.Election).FirstOrDefault(n => n.ElectionNominationID == nominationId && n.ResidenceElectionID == id); if (nomination == null) return HttpNotFound();
            if (!IsAdmin() && (int?)Session["ResidenceID"] != nomination.Election.ResidenceID) return new HttpStatusCodeResult(403);
            var reviewAllowed = nomination.Election.Status == "Applications Open" ||
                                nomination.Election.Status == "Candidate Review" ||
                                nomination.Election.Status == "Campaign" ||
                                nomination.Election.Status == "Candidate Review Required";
            if (!reviewAllowed)
            {
                TempData["Error"] = "Applications are locked once voting has opened.";
                return RedirectToAction("Review", new { id });
            }
            if (decision != "Approve" && decision != "Info" && decision != "Reject") return new HttpStatusCodeResult(400, "Invalid review decision.");
            if (decision == "Info" && string.IsNullOrWhiteSpace(note)) { TempData["Error"] = "Enter the information required before sending an information request."; return RedirectToAction("Review", new { id }); }
            var eligibility = workflow.EvaluateEligibility(db, nomination.Election, nomination.StudentID, nomination.ElectionNominationID);
            nomination.EligibilityRecommendation = eligibility.Recommendation;
            nomination.EligibilityReason = eligibility.Reason;
            if (decision == "Approve" && eligibility.Recommendation == "Not Eligible") { nomination.Status = "Rejected"; nomination.ReviewNote = eligibility.Reason; nomination.ReviewedAt = DateTime.Now; nomination.ReviewedByStaffID = (int)Session["StaffID"]; workflow.Audit(db, id, "ApplicationRejected", "Approval blocked: " + eligibility.Reason, "Staff", (int)Session["StaffID"]); db.SaveChanges(); TempData["Error"] = "Approval was blocked because the applicant is no longer eligible: " + eligibility.Reason; return RedirectToAction("Review", new { id }); }
            nomination.Status = decision == "Approve" ? "Approved" : decision == "Info" ? "Pending Candidate Response" : "Rejected";
            nomination.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(); nomination.ReviewedAt = DateTime.Now; nomination.ReviewedByStaffID = (int)Session["StaffID"];
            workflow.Audit(db, id, nomination.Status == "Approved" ? "ApplicationApproved" : "ApplicationReviewed", nomination.Status, "Staff", (int)Session["StaffID"]);
            if (nomination.Status == "Approved") workflow.NotifyCandidateApprovalCompleted(db, nomination.Election);
            string message = decision == "Info" ? "Additional information is required for your House Committee application: " + nomination.ReviewNote : "Your House Committee application has been " + nomination.Status.ToLowerInvariant() + (string.IsNullOrWhiteSpace(nomination.ReviewNote) ? "." : ": " + nomination.ReviewNote);
            db.Notifications.Add(new Notification { UserID = nomination.StudentID, UserType = "Student", Title = "House Committee application update", Message = message, NotificationType = "Election", RelatedID = nomination.ElectionNominationID, RelatedType = "ElectionApplication", IsRead = false, ExpiryDate = DateTime.Now.AddDays(14) });
            db.SaveChanges();
            var applicant = db.Students.Find(nomination.StudentID);
            if (applicant != null) new NotificationService().SendEmail(applicant.Email, "DUT Housing: House Committee application update", message);
            TempData["Success"] = decision == "Info" ? "Information request sent to the applicant." : "Application decision updated."; return RedirectToAction("Review", new { id });
        }

        public ActionResult Ballot(int id)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth"); workflow.RunDueWorkflows();
            var election = db.ResidenceElections.Include(e => e.Positions).FirstOrDefault(e => e.ResidenceElectionID == id); if (election == null) return HttpNotFound();
            if (election.Status != "Voting Open" || student.ResidenceID != election.ResidenceID) return new HttpStatusCodeResult(403);
            var positions = election.Positions.OrderBy(p => p.DisplayOrder).ToList(); var nominations = db.ElectionNominations.Include(n => n.Student).Where(n => n.ResidenceElectionID == id && n.Status == "Approved").ToList();
            if (db.ElectionParticipations.Count(p => p.ResidenceElectionID == id && p.StudentID == student.StudentID) >= positions.Count)
            {
                TempData["Success"] = "Your ballot has already been recorded.";
                return RedirectToAction("Details", new { id });
            }
            return View(new BallotViewModel { Election = election, Positions = positions, Candidates = nominations.GroupBy(n => n.ElectionPositionID).ToDictionary(g => g.Key, g => g.ToList()) });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Ballot(int id, Dictionary<int, int> selections)
        {
            var student = CurrentStudent(); if (student == null) return RedirectToAction("StudentLogin", "Auth"); workflow.RunDueWorkflows(); var election = db.ResidenceElections.Find(id); if (election == null) return HttpNotFound();
            var positions = db.ElectionPositions.Where(p => p.ResidenceElectionID == id).ToList();
            var submittedSelections = selections ?? new Dictionary<int, int>();
            // MVC5 does not bind dictionary keys consistently for all browser/form
            // combinations, so always recover the keyed radio values from the form.
            foreach (var position in positions)
            {
                var submittedValue = Request.Form["selections[" + position.ElectionPositionID + "]"];
                int nominationId;
                if (Int32.TryParse(submittedValue, out nominationId))
                    submittedSelections[position.ElectionPositionID] = nominationId;
            }
            if (positions.Any(p => !submittedSelections.ContainsKey(p.ElectionPositionID))) { TempData["Error"] = "Select one candidate for every position."; return RedirectToAction("Ballot", new { id }); }
            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    foreach (var position in positions) { string error; if (!workflow.CastVote(db, election, position.ElectionPositionID, submittedSelections[position.ElectionPositionID], student.StudentID, out error)) { transaction.Rollback(); TempData["Error"] = error; return RedirectToAction("Ballot", new { id }); } }
                    db.SaveChanges(); transaction.Commit();
                }
                TempData["Success"] = "Your anonymous ballot has been securely recorded."; return RedirectToAction("Details", new { id });
            }
            catch { TempData["Error"] = "Your ballot could not be recorded. No partial ballot was saved."; return RedirectToAction("Details", new { id }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResolveTie(int id, int positionId, int nominationId)
        {
            if (!IsAdmin()) return new HttpStatusCodeResult(403);
            var election = db.ResidenceElections.Find(id); if (election == null) return HttpNotFound();
            string error;
            if (!workflow.ResolveTie(db, election, positionId, nominationId, (int)Session["StaffID"], out error)) { TempData["Error"] = error; return RedirectToAction("Review", new { id }); }
            db.SaveChanges(); TempData["Success"] = "Tie decision recorded."; return RedirectToAction("Details", new { id });
        }

        public ActionResult Dashboard(int id)
        {
            if (!IsManager()) return new HttpStatusCodeResult(403); workflow.RunDueWorkflows(); var election = db.ResidenceElections.Include(e => e.Residence).FirstOrDefault(e => e.ResidenceElectionID == id); if (election == null) return HttpNotFound();
            if (!IsAdmin() && (int?)Session["ResidenceID"] != election.ResidenceID) return new HttpStatusCodeResult(403);
            int voters = db.Students.Count(s => s.IsActive && s.ResidenceID == election.ResidenceID); int cast = db.ElectionParticipations.Where(p => p.ResidenceElectionID == id).Select(p => p.StudentID).Distinct().Count();
            return View(new ElectionDashboardViewModel { Election = election, EligibleVoters = voters, BallotsCast = cast, Nominations = db.ElectionNominations.Count(n => n.ResidenceElectionID == id), ApprovedCandidates = db.ElectionNominations.Count(n => n.ResidenceElectionID == id && n.Status == "Approved"), Turnout = voters == 0 ? 0 : Math.Round((decimal)cast * 100 / voters, 1) });
        }

        private Student CurrentStudent() { return Session["StudentID"] == null ? null : db.Students.Find((int)Session["StudentID"]); }
        private bool IsAdmin() { return Session["StaffID"] != null && (Session["UserType"] as string) == "Admin"; }
        private bool IsManager() { var role = Session["UserType"] as string; return Session["StaffID"] != null && (role == "Admin" || role == "Building Manager"); }
    }
}
