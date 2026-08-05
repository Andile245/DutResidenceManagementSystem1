using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace DUTResManagementSystem.ViewModels
{
    public class MessageViewModel
    {
        public int MessageId { get; set; }            // Unique ID for the message
        public string Subject { get; set; }           // Message subject/title
        public string Content { get; set; }           // Message body/content
        public string SenderName { get; set; }        // Who sent the message
        public string SenderRole { get; set; }        // Role of the sender (Admin, Staff, etc.)
        public bool IsUrgent { get; set; }            // Flag for urgent messages
        public bool IsRead { get; set; }              // Whether the message has been read
        public DateTime SentDate { get; set; }        // When the message was sent
    }
}
