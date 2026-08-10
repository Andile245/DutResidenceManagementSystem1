using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.ViewModels
{
    public class ElectionSetupViewModel
    {
        [Required, StringLength(120)] public string Title { get; set; }
        [Required] public int ResidenceID { get; set; }
        [Required] public DateTime NominationOpensAt { get; set; }
        [Required] public DateTime NominationClosesAt { get; set; }
        [Required] public DateTime CampaignOpensAt { get; set; }
        [Required] public DateTime VotingOpensAt { get; set; }
        [Required] public DateTime VotingClosesAt { get; set; }
        [Required] public DateTime ResultsPublicationAt { get; set; }
        [Range(0, 100)] public decimal? MinimumTurnoutPercentage { get; set; }
        public bool PreventSelfVote { get; set; } = true;
        [Required, StringLength(500)] public string Positions { get; set; }
    }
    public class NominationInputViewModel
    {
        [Required] public int ElectionPositionID { get; set; }
        [Required, StringLength(1500)] public string Manifesto { get; set; }
        [Required, StringLength(1000)] public string Motivation { get; set; }
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the election rules.")]
        public bool AcceptedElectionRules { get; set; }
    }
    public class ElectionScheduleViewModel
    {
        public int ElectionId { get; set; }
        public string Title { get; set; }
        [Required] public DateTime NominationOpensAt { get; set; }
        [Required] public DateTime NominationClosesAt { get; set; }
        [Required] public DateTime CampaignOpensAt { get; set; }
        [Required] public DateTime VotingOpensAt { get; set; }
        [Required] public DateTime VotingClosesAt { get; set; }
        [Required] public DateTime ResultsPublicationAt { get; set; }
    }
    public class ElectionDashboardViewModel
    {
        public ResidenceElection Election { get; set; }
        public int EligibleVoters { get; set; }
        public int BallotsCast { get; set; }
        public int Nominations { get; set; }
        public int ApprovedCandidates { get; set; }
        public decimal Turnout { get; set; }
        public List<ElectionAuditLog> AuditLogs { get; set; }
        public List<Notification> Notifications { get; set; }
    }
    public class BallotViewModel
    {
        public ResidenceElection Election { get; set; }
        public List<ElectionPosition> Positions { get; set; }
        public Dictionary<int, List<ElectionNomination>> Candidates { get; set; }
    }
}
