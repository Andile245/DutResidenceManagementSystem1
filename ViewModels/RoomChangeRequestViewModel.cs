using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;
using DUTResManagementSystem.Models;

namespace DUTResManagementSystem.ViewModels
{
    public class RoomChangeRequestViewModel
    {
        [Required(ErrorMessage = "Please provide a reason for your request")]
        [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
        [Display(Name = "Reason for Room Change")]
        public string Reason { get; set; }

        // Optional supporting document
        [Display(Name = "Supporting Document (Optional)")]
        public HttpPostedFileBase DocumentFile { get; set; }
    }

    public class ReviewRoomChangeViewModel
    {
        public int RequestID { get; set; }

        [Required(ErrorMessage = "Please select a decision")]
        public string Decision { get; set; } // "Approved" or "Declined"

        [StringLength(500)]
        [Display(Name = "Feedback to Student")]
        public string AdminFeedback { get; set; }

        // If approved, the building manager can assign a specific room
        [Display(Name = "Assign Room")]
        public int? NewRoomID { get; set; }

        public SelectList AvailableRooms { get; set; }
    }
}
