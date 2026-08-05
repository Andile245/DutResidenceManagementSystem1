// Models/Notification.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace DUTResManagementSystem.Models
{
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [StringLength(50)]
        public string UserType { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; }

        [Required]
        [StringLength(50)]
        public string NotificationType { get; set; }

        public int? RelatedID { get; set; }

        [StringLength(50)]
        public string RelatedType { get; set; }

        public bool? IsRead { get; set; }

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime? ExpiryDate { get; set; }
    }
}