using System;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class Announcement
    {
        [Key]
        public int AnnouncementID { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(1000)]
        public string Content { get; set; }

        [Required]
        public int StaffID { get; set; }
        public virtual Staff Staff { get; set; }

        [Required]
        public DateTime DatePosted { get; set; } = DateTime.Now;

        [Required]
        public DateTime? ExpiryDate { get; set; }

        [Required]
        public string Priority { get; set; } // e.g., Normal, High, Emergency

        [Required]
        public string TargetAudience { get; set; } // e.g., Students, Staff, Everyone
        public int? ResidenceID { get; set; }
        public virtual Residence Residence { get; set; }
    }
}