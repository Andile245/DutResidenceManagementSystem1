using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Web;

namespace DUTResManagementSystem.ViewModels
{
    public class StudentDashboardViewModel
    {
        public int StudentID { get; set; }
        public int? ResidenceID { get; set; }

        [ForeignKey("ResidenceID")]
        public virtual Residence Residence { get; set; }

        public Student Student { get; set; }
        public List<Maintenance> MaintenanceRequests { get; set; }
        public List<Announcement> Announcements { get; set; }
        public int UnreadAnnouncementCount { get; set; }
        public List<Announcement> UnreadAnnouncements { get; set; }
    }

    public class MaintenanceReportViewModel
    {
        [Required]
        [StringLength(50)]
        public string RoomNumber { get; set; } // Student inputs room
        public MaintenanceIssueType IssueType { get; set; }

        [Required(ErrorMessage = "Please describe the maintenance issue")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Issue Description")]
        public string IssueDescription { get; set; }

        public HttpPostedFileBase ImageFile { get; set; }
    }

}