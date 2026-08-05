using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    public class RoomInspection
    {
        [Key]
        public int RoomInspectionID { get; set; }

        [Required]
        public int RoomID { get; set; }

        [ForeignKey("RoomID")]
        public virtual Room Room { get; set; }

        public int? StudentID { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }

        [Required]
        [StringLength(20)]
        public string InspectionType { get; set; }

        [Required]
        [StringLength(30)]
        public string ConditionStatus { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }

        public bool RequiresMaintenance { get; set; }

        public bool BlocksAllocation { get; set; }

        public DateTime InspectionDate { get; set; } = DateTime.Now;

        public int? InspectedByStaffID { get; set; }

        [ForeignKey("InspectedByStaffID")]
        public virtual Staff InspectedBy { get; set; }
    }
}
