

using DUTResManagementSystem.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class Student
    {
        [Key]
        public int StudentID { get; set; }

        [Required]
        public string StudentNumber { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string PasswordHash { get; set; }


        public string PhoneNumber { get; set; }

        [Required]
        public string Faculty { get; set; }

        [Range(1, 10)]
        public int YearOfStudy { get; set; }

        public int? ResidenceID { get; set; }
        public virtual Residence Residence { get; set; }

        public int? RoomID { get; set; }
        public virtual Room Room { get; set; }

        public DateTime DateRegistered { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        [Required]
        public string Gender { get; set; }

        public string FundingType { get; set; } = "Self-Paying";
    }
}

