using DUTResManagementSystem.Models;
using System;
using System.ComponentModel.DataAnnotations;

public enum MaintenanceIssueType
{
    Plumbing,
    Electrical,
    Internet,
    Cleaning,
    Furniture,
    Security,
    Other
}

namespace DUTResManagementSystem.Models
{
   
    public class Maintenance
    {
        [Key]
        public int MaintenanceID { get; set; }

        [Required]
        public int StudentID { get; set; }
        public virtual Student Student { get; set; }

        public int? RoomID { get; set; }
        public virtual Room Room { get; set; }
        public MaintenanceIssueType IssueType { get; set; }

        [Required]
        [StringLength(500)]
        public string IssueDescription { get; set; }

        [Required]
        public DateTime DateReported { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        public string RoomNumber { get; set; } // New field

        [Required]
        public string Status { get; set; } = "Pending"; // e.g., Pending, In Progress, Resolved

        public DateTime? DateResolved { get; set; }
        public bool IsConfirmedByStudent { get; set; }

        public string ImagePath { get; set; }
        public int? StaffID { get; set; } // nullable
        public virtual Staff Staff { get; set; }

        public int? TechnicianID { get; set; }
        public virtual Technician Technician { get; set; }
        public string CompletionImage { get; set; }

        [StringLength(20)]
        public string Priority { get; set; } = "Normal";

        public DateTime? TargetResponseBy { get; set; }

        public DateTime? EscalatedAt { get; set; }

        [StringLength(500)]
        public string EscalationReason { get; set; }

        public bool IsSafetyCritical { get; set; }
    }
}
