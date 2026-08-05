using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    public class ResidenceElection
    {
        [Key] public int ResidenceElectionID { get; set; }
        [Required, StringLength(120)] public string Title { get; set; }
        [Required] public int ResidenceID { get; set; }
        [ForeignKey("ResidenceID")] public virtual Residence Residence { get; set; }
        [Required] public DateTime NominationOpensAt { get; set; }
        [Required] public DateTime NominationClosesAt { get; set; }
        [Required] public DateTime CampaignOpensAt { get; set; }
        [Required] public DateTime VotingOpensAt { get; set; }
        [Required] public DateTime VotingClosesAt { get; set; }
        [Required] public DateTime ResultsPublicationAt { get; set; }
        [Range(0, 100)] public decimal? MinimumTurnoutPercentage { get; set; }
        [Required, StringLength(30)] public string Status { get; set; } = "Scheduled";
        public bool PreventSelfVote { get; set; } = true;
        public int CreatedByStaffID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ResultsPublishedAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public virtual ICollection<ElectionPosition> Positions { get; set; }
    }

    public class ElectionPosition
    {
        [Key] public int ElectionPositionID { get; set; }
        [Required] public int ResidenceElectionID { get; set; }
        [ForeignKey("ResidenceElectionID")] public virtual ResidenceElection Election { get; set; }
        [Required, StringLength(80)] public string Name { get; set; }
        [Range(1, 10)] public int Seats { get; set; } = 1;
        public int DisplayOrder { get; set; }
        public virtual ICollection<ElectionNomination> Nominations { get; set; }
    }

    public class ElectionNomination
    {
        [Key] public int ElectionNominationID { get; set; }
        [Required] public int ResidenceElectionID { get; set; }
        [Required] public int ElectionPositionID { get; set; }
        [Required] public int StudentID { get; set; }
        [ForeignKey("StudentID")] public virtual Student Student { get; set; }
        [ForeignKey("ResidenceElectionID")] public virtual ResidenceElection Election { get; set; }
        [ForeignKey("ElectionPositionID")] public virtual ElectionPosition Position { get; set; }
        [Required, StringLength(30)] public string EligibilityRecommendation { get; set; }
        [StringLength(1000)] public string EligibilityReason { get; set; }
        [Required, StringLength(30)] public string Status { get; set; } = "Pending";
        [Required, StringLength(1500)] public string Manifesto { get; set; }
        [Required, StringLength(1000)] public string Motivation { get; set; }
        [StringLength(300)] public string ProfilePhotoPath { get; set; }
        [StringLength(300)] public string CampaignImagePath { get; set; }
        public bool AcceptedElectionRules { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        [StringLength(500)] public string WithdrawalReason { get; set; }
        [StringLength(500)] public string ReviewNote { get; set; }
        public int? ReviewedByStaffID { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
        public DateTime? ReviewedAt { get; set; }
        public bool IsWinner { get; set; }
        public int? VoteCount { get; set; }
    }

    // Deliberately separated from ElectionVote so a ballot cannot be joined to a voter.
    public class ElectionParticipation
    {
        [Key] public int ElectionParticipationID { get; set; }
        [Required] public int ResidenceElectionID { get; set; }
        [Required] public int ElectionPositionID { get; set; }
        [Required] public int StudentID { get; set; }
        public DateTime CastAt { get; set; } = DateTime.Now;
    }

    public class ElectionVote
    {
        [Key] public int ElectionVoteID { get; set; }
        [Required] public int ResidenceElectionID { get; set; }
        [Required] public int ElectionPositionID { get; set; }
        [Required] public int ElectionNominationID { get; set; }
        [ForeignKey("ElectionNominationID")] public virtual ElectionNomination Nomination { get; set; }
        public DateTime CastAt { get; set; } = DateTime.Now;
    }

    // This is the active committee roster, not a duplicate student account.
    public class CommitteeAppointment
    {
        [Key] public int CommitteeAppointmentID { get; set; }
        [Required] public int ResidenceElectionID { get; set; }
        [Required] public int ElectionPositionID { get; set; }
        [Required] public int StudentID { get; set; }
        [ForeignKey("StudentID")] public virtual Student Student { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime AppointedAt { get; set; } = DateTime.Now;
        public DateTime? EndedAt { get; set; }
    }

    public class ElectionAuditLog
    {
        [Key] public int ElectionAuditLogID { get; set; }
        public int? ResidenceElectionID { get; set; }
        [Required, StringLength(80)] public string EventType { get; set; }
        [Required, StringLength(1000)] public string Detail { get; set; }
        [StringLength(30)] public string ActorType { get; set; }
        public int? ActorID { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }
}
