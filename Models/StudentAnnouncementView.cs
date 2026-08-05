// Models/StudentAnnouncementView.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class StudentAnnouncementView
    {
        [Key]
        public int ViewID { get; set; }

        [Required]
        public int StudentID { get; set; }
        public virtual Student Student { get; set; }

        [Required]
        public int AnnouncementID { get; set; }
        public virtual Announcement Announcement { get; set; }

        [Required]
        public DateTime DateViewed { get; set; } = DateTime.Now;
    }
}