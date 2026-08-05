using System.Collections.Generic;

namespace DUTResManagementSystem.ViewModels
{
    public class VisitorDashboardViewModel
    {
        public string ResidenceName { get; set; }
        public int ActiveVisitorCount { get; set; }
        public int PendingEntryCount { get; set; }
        public int TotalVisitsToday { get; set; }
        public int ExitsToday { get; set; }
        public int UniqueStudentsVisitedToday { get; set; }
        public int CurfewAlertCount { get; set; }
        public List<VisitorRecordViewModel> ActiveVisitors { get; set; } = new List<VisitorRecordViewModel>();
        public List<VisitorRecordViewModel> OpenVisitorPasses { get; set; } = new List<VisitorRecordViewModel>();
        public VisitorRecordViewModel LatestGeneratedVisitor { get; set; }
    }
}
