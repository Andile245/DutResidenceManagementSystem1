using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    public class Complaint
    {
        [Key]
        public int ComplaintId { get; set; }

        [Display(Name = "Student")]
        public int? StudentID { get; set; }

        [Display(Name = "Reported student")]
        public int? ReportedStudentID { get; set; }

        [Required(ErrorMessage = "Please enter a subject for your complaint.")]
        [StringLength(120, ErrorMessage = "The subject cannot be longer than 120 characters.")]
        [Display(Name = "Subject")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please select a complaint category.")]
        [StringLength(200, ErrorMessage = "The category cannot be longer than 200 characters.")]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Please describe your complaint.")]
        [StringLength(1000, ErrorMessage = "The complaint description cannot be longer than 1000 characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; }

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        [StringLength(500)]
        [Display(Name = "Manager Feedback")]
        public string ManagerFeedback { get; set; }

        public DateTime? LastUpdated { get; set; }

        public DateTime? DateResolved { get; set; }

        [Display(Name = "Reviewed By")]
        public int? ReviewedByStaffID { get; set; }

        [StringLength(20)]
        public string Priority { get; set; } = "Normal";

        public DateTime? TargetResolutionBy { get; set; }

        public DateTime? EscalatedAt { get; set; }

        [StringLength(500)]
        public string EscalationReason { get; set; }

        public bool WarningIssued { get; set; }

        [StringLength(20)]
        public string WarningSeverity { get; set; }

        [StringLength(500)]
        public string WarningReason { get; set; }

        public DateTime? WarningIssuedAt { get; set; }

        public int? WarningIssuedByStaffID { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }

        [ForeignKey("ReportedStudentID")]
        public virtual Student ReportedStudent { get; set; }

        [ForeignKey("ReviewedByStaffID")]
        public virtual Staff ReviewedByStaff { get; set; }
    }

    // A durable student-history entry created when a complaint results in a warning.
    public class StudentConductRecord
    {
        [Key] public int StudentConductRecordID { get; set; }
        [Required] public int StudentID { get; set; }
        [Required] public int ComplaintId { get; set; }
        [Required, StringLength(20)] public string Severity { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime IssuedAt { get; set; } = DateTime.Now;
        public int IssuedByStaffID { get; set; }
        public DateTime? ResolvedAt { get; set; }
        [ForeignKey("StudentID")] public virtual Student Student { get; set; }
        [ForeignKey("ComplaintId")] public virtual Complaint Complaint { get; set; }
        [ForeignKey("IssuedByStaffID")] public virtual Staff IssuedByStaff { get; set; }
    }
}
