using System;

namespace DUTResManagementSystem.ViewModels
{
    public class ComplaintManagementViewModel
    {
        public int ComplaintId { get; set; }
        public int? StudentID { get; set; }
        public int? ReportedStudentID { get; set; }
        public string Subject { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string ManagerFeedback { get; set; }
        public DateTime DateSubmitted { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? DateResolved { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public string ReportedStudentName { get; set; }
        public string ReportedStudentNumber { get; set; }
        public string ResidenceName { get; set; }
        public int? ResidenceID { get; set; }
        public bool WarningIssued { get; set; }
        public string WarningSeverity { get; set; }
        public string WarningReason { get; set; }
    }
}
