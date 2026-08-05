using System;

namespace DUTResManagementSystem.ViewModels
{
    public class ComplaintSummaryViewModel
    {
        public int ComplaintId { get; set; }
        public string Subject { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string ManagerFeedback { get; set; }
        public DateTime DateSubmitted { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? DateResolved { get; set; }
    }
}
