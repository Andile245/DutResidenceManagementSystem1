
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.Models
{
    public class Technician
    {
        [Key]
        public int TechnicianID { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [StringLength(50)]
        public string TechnicianType { get; set; }
        // Examples: Electrician, Plumber, Carpenter, Painter, Cleaner

        [Required]
        [StringLength(10)]
        public string PhoneNumber { get; set; }

        [StringLength(100)]
        public string Email { get; set; }
        public string Password { get; set; }

        [Required]
        public bool AvailabilityStatus { get; set; } = true;
        // true = Available, false = Busy

        public DateTime DateAdded { get; set; } = DateTime.Now;

        // Navigation Property
        public virtual ICollection<Maintenance> Maintenances { get; set; }
    }
}