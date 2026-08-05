using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DUTResManagementSystem.Models
{
    // ── Mirrors RoomChangeRequest exactly but for residence transfers ──────────
    public class ResidenceChangeRequest
    {
        [Key]
        public int RequestID { get; set; }

        // ── STUDENT ───────────────────────────────────────────────────────────
        [Required]
        public int StudentID { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }

        // ── CURRENT RESIDENCE ─────────────────────────────────────────────────
        [Required]
        public int CurrentResidenceID { get; set; }

        [ForeignKey("CurrentResidenceID")]
        public virtual Residence CurrentResidence { get; set; }

        // ── REASON & DOCUMENT ─────────────────────────────────────────────────
        [Required]
        [StringLength(1000)]
        public string Reason { get; set; }

        // Optional supporting document path (medical cert, letter, etc.)
        public string DocumentPath { get; set; }

        // ── STATUS ────────────────────────────────────────────────────────────
        // Pending → Approved / Declined
        [Required]
        public string Status { get; set; } = "Pending";

        // Written feedback from admin
        public string AdminFeedback { get; set; }

        // ── DATES ─────────────────────────────────────────────────────────────
        public DateTime DateRequested { get; set; } = DateTime.Now;
        public DateTime? DateReviewed { get; set; }

        // ── AUDIT ─────────────────────────────────────────────────────────────
        public int? ReviewedByStaffID { get; set; }

        [ForeignKey("ReviewedByStaffID")]
        public virtual Staff ReviewedBy { get; set; }
    }
}
