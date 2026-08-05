using DUTResManagementSystem.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class Room
    {
        [Key]
        public int RoomID { get; set; }

        [Required]
        public int ResidenceID { get; set; }
        public virtual Residence Residence { get; set; }

        [Required]
        [StringLength(20)]
        public string RoomNumber { get; set; }

        // "Single", "Double", "Triple"
        [Required]
        public string RoomType { get; set; }

        // "Available" or "Occupied"
        [Required]
        public string Status { get; set; }

        // "Male", "Female", "Mixed"
        [Required]
        public string Gender { get; set; }

        [Range(1, 5)]
        public int Capacity { get; set; }

        public int? Floor { get; set; }

        // Navigation — students currently in this room
        public virtual ICollection<Student> Students { get; set; }
    }
}
