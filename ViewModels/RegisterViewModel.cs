using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace DUTResManagementSystem.ViewModels
{
    public class RegisterViewModel
    {
        // Common fields
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        [Phone]
        [MinLength(10, ErrorMessage ="Phone number must have 10 digits!"), MaxLength(10)]
        public string PhoneNumber { get; set; }

        // Student-specific fields
        [Display(Name = "Student Number")]
        public string StudentNumber { get; set; }

        [Display(Name = "Faculty")]
        public string Faculty { get; set; }

        [Display(Name = "Year of Study")]
        [Range(1, 5, ErrorMessage = "Year of study must be between 1 and 5")]
        public int? YearOfStudy { get; set; }

        // Staff-specific fields
        [Display(Name = "Staff Number")]
        public string StaffNumber { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        // Registration type
        [Required]
        [Display(Name = "I am a")]
        public string UserType { get; set; } // "Student" or "Staff"
        public string Gender { get;  set; }
        public int? ResidenceID { get; set; }
    }
}