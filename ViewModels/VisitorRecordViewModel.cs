using System;

namespace DUTResManagementSystem.ViewModels
{
    public class VisitorRecordViewModel
    {
        public int VisitorID { get; set; }
        public string FullName { get; set; }
        public string IDNumber { get; set; }
        public string DocumentType { get; set; }
        public string StudentDisplayName { get; set; }
        public DateTime PassCreatedAt { get; set; }
        public DateTime? EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public string RoomNumber { get; set; }
        public bool IsActive { get; set; }
        public string CurrentStatus { get; set; }
        public string QRCode { get; set; }
        public string QrCodeSvg { get; set; }
    }
}
