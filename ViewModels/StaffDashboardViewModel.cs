using DUTResManagementSystem.Models;
using System.Collections.Generic;

namespace DUTResManagementSystem.ViewModels
{
    public class StaffDashboardViewModel
    {

        public Staff Staff { get; set; }
        public List<Announcement> Announcements { get; set; }
        public List<Maintenance> MaintenanceRequests { get; set; }
        public List<Residence> Residences { get; set; }
        public List<Notification> Notifications { get; set; }
        public int TotalStudents { get; set; }
        public int AvailableRooms { get; set; }
        public int PendingMaintenance { get; set; }
        public string UserType { get; set; }
        public string Role { get; set; }
        public int? ResidenceID { get; set; }

    }
}