using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.Models
{
    public class Visitor
    {
        [Key]
        public int VisitorID { get; set; }

        [Required]
        [Display(Name = "Visitor Full Name")]
        public string FullName { get; set; }

        [Required]
        [Display(Name = "Document Number")]
        [RegularExpression(@"^[A-Za-z0-9\-\/ ]{4,40}$", ErrorMessage = "Document number must be 4 to 40 letters or digits.")]
        public string IDNumber { get; set; }

        [Required]
        [Display(Name = "Document Type")]
        public string DocumentType { get; set; }

        [Required]
        [Display(Name = "Pass Created At")]
        public DateTime CheckInTime { get; set; }

        public DateTime? EntryTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        [Required]
        [Display(Name = "Student To Visit")]
        public int StudentID { get; set; }

        public int ResidenceID { get; set; }

        public string QRCode { get; set; }

        public bool IsActive { get; set; }

        public bool CurfewAlertSent { get; set; }

        public DateTime? IdentityVerifiedAt { get; set; }

        public bool IdentityVerified { get; set; }

        public DateTime? OverstayAlertSentAt { get; set; }

        public bool IsOverstayFlagged { get; set; }
    }
}
