using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.ViewModels
{
    public class ResidenceChangeRequestViewModel
    {
        [Required(ErrorMessage = "Please provide a reason for your request")]
        [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
        [Display(Name = "Reason for Residence Change")]
        public string Reason { get; set; }

        [Display(Name = "Supporting Document (Optional)")]
        public HttpPostedFileBase DocumentFile { get; set; }

    }
}
