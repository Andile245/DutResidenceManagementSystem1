using System;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class RoomChangeRequest
    {
        [Key]
        public int RequestID { get; set; }

        [Required]
        public int StudentID { get; set; }
        public virtual Student Student { get; set; }

        // The room the student is currently in
        [Required]
        public int CurrentRoomID { get; set; }
        public virtual Room CurrentRoom { get; set; }

        // The room the student wants to move to (optional — student may not know)
        public int? RequestedRoomID { get; set; }
        public virtual Room RequestedRoom { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Reason for Request")]
        public string Reason { get; set; }

        // Optional supporting document (e.g. medical certificate, letter)
        public string DocumentPath { get; set; }

        // "Pending", "Approved", "Declined"
        [Required]
        public string Status { get; set; } = "Pending";

        // Building manager feedback when approving or declining
        [StringLength(500)]
        [Display(Name = "Admin Feedback")]
        public string AdminFeedback { get; set; }

        public DateTime DateRequested { get; set; } = DateTime.Now;

        public DateTime? DateReviewed { get; set; }

        // The staff member who reviewed the request
        public int? ReviewedByStaffID { get; set; }
        public virtual Staff ReviewedBy { get; set; }
    }
}
