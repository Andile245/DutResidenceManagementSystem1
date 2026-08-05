using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    public class EmergencyRollCallPerson
    {
        [Key]
        public int EmergencyRollCallPersonID { get; set; }

        [Required]
        public int EmergencyRollCallID { get; set; }

        [ForeignKey("EmergencyRollCallID")]
        public virtual EmergencyRollCall EmergencyRollCall { get; set; }

        [Required]
        [StringLength(20)]
        public string PersonType { get; set; }

        public int? StudentID { get; set; }

        public int? VisitorID { get; set; }

        [Required]
        [StringLength(120)]
        public string DisplayName { get; set; }

        [StringLength(80)]
        public string RoomNumber { get; set; }

        [Required]
        [StringLength(20)]
        public string SafetyStatus { get; set; } = "Unknown";

        [StringLength(500)]
        public string Notes { get; set; }

        public DateTime? MarkedAt { get; set; }

        public int? MarkedByStaffID { get; set; }
    }
}
