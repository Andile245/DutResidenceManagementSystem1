using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class Residence
    {
        [Key]
        public int ResidenceID { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(200)]
        public string Address { get; set; }

        [Range(1, 1000)]
        public int Capacity { get; set; }

        [Required]
        public string GenderPolicy { get; set; } // e.g., Male, Female, Mixed

        [DataType(DataType.PhoneNumber)]
        [StringLength(15, MinimumLength = 10)]
        public string ContactNumber { get; set; }

        [Required]
        public string Faculty { get; set; }

        public int CurrentOccupancy { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<Room> Rooms { get; set; }
        public virtual ICollection<Student> Students { get; set; }
    }
}