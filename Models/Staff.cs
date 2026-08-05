using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.Models
{
    public class Staff
    {
        [Key]
        public int StaffID { get; set; } 
        [Required]
        public string StaffNumber { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Required]
        
        [StringLength(15, MinimumLength = 10)]
        public string PhoneNumber { get; set; }
        [Required]
        public string Role { get; set; }
        public DateTime DateRegistered { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } 
        public virtual Residence Residence { get; set; }
        public int? ResidenceID { get; set; }
    }
}