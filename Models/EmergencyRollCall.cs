using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    public class EmergencyRollCall
    {
        [Key]
        public int EmergencyRollCallID { get; set; }

        [Required]
        public int ResidenceID { get; set; }

        [ForeignKey("ResidenceID")]
        public virtual Residence Residence { get; set; }

        [Required]
        [StringLength(120)]
        public string IncidentTitle { get; set; }

        [StringLength(1000)]
        public string IncidentNotes { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Open";

        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime? ClosedAt { get; set; }

        public int? StartedByStaffID { get; set; }

        [ForeignKey("StartedByStaffID")]
        public virtual Staff StartedBy { get; set; }

        public virtual ICollection<EmergencyRollCallPerson> People { get; set; }
    }
}
