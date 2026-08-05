using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    public class ResidenceCheckIn
    {
        [Key]
        public int CheckInID { get; set; }

        // Which student this QR belongs to
        [Required]
        public int StudentID { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }

        // Which residence this QR is for
        [Required]
        public int ResidenceID { get; set; }

        [ForeignKey("ResidenceID")]
        public virtual Residence Residence { get; set; }

        // Unique token embedded in the QR code URL
        // Generated as: HMAC-SHA256(StudentID:ResidenceID:SecretKey)
        [Required]
        public string QRToken { get; set; }

        // ── CHECK-IN ──────────────────────────────────────────────
        public bool HasCheckedIn { get; set; } = false;
        public DateTime? CheckInTime { get; set; }

        // ── CHECK-OUT (logging only, does not affect room allocation)
        public bool HasCheckedOut { get; set; } = false;
        public DateTime? CheckOutTime { get; set; }

        // ── AUDIT ─────────────────────────────────────────────────
        public DateTime TokenGeneratedAt { get; set; } = DateTime.Now;

        public int? GeneratedByStaffID { get; set; }

        [ForeignKey("GeneratedByStaffID")]
        public virtual Staff GeneratedBy { get; set; }
    }
}
