namespace DUTResManagementSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Allocations",
                c => new
                    {
                        AllocationID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(),
                        ResidenceID = c.Int(),
                        RoomNumber = c.String(),
                    })
                .PrimaryKey(t => t.AllocationID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID)
                .ForeignKey("dbo.Students", t => t.StudentID)
                .Index(t => t.StudentID)
                .Index(t => t.ResidenceID);
            
            CreateTable(
                "dbo.Residences",
                c => new
                    {
                        ResidenceID = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Address = c.String(nullable: false, maxLength: 200),
                        Capacity = c.Int(nullable: false),
                        GenderPolicy = c.String(nullable: false),
                        ContactNumber = c.String(maxLength: 15),
                        Faculty = c.String(nullable: false),
                        CurrentOccupancy = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ResidenceID);
            
            CreateTable(
                "dbo.Rooms",
                c => new
                    {
                        RoomID = c.Int(nullable: false, identity: true),
                        ResidenceID = c.Int(nullable: false),
                        RoomNumber = c.String(nullable: false, maxLength: 20),
                        RoomType = c.String(nullable: false),
                        Status = c.String(nullable: false),
                        Gender = c.String(nullable: false),
                        Capacity = c.Int(nullable: false),
                        Floor = c.Int(),
                    })
                .PrimaryKey(t => t.RoomID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID, cascadeDelete: true)
                .Index(t => t.ResidenceID);
            
            CreateTable(
                "dbo.Students",
                c => new
                    {
                        StudentID = c.Int(nullable: false, identity: true),
                        StudentNumber = c.String(nullable: false),
                        FirstName = c.String(nullable: false),
                        LastName = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        PasswordHash = c.String(nullable: false),
                        PhoneNumber = c.String(),
                        Faculty = c.String(nullable: false),
                        YearOfStudy = c.Int(nullable: false),
                        ResidenceID = c.Int(),
                        RoomID = c.Int(),
                        DateRegistered = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        Gender = c.String(nullable: false),
                        FundingType = c.String(),
                    })
                .PrimaryKey(t => t.StudentID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID)
                .ForeignKey("dbo.Rooms", t => t.RoomID)
                .Index(t => t.ResidenceID)
                .Index(t => t.RoomID);
            
            CreateTable(
                "dbo.Announcements",
                c => new
                    {
                        AnnouncementID = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 100),
                        Content = c.String(nullable: false, maxLength: 1000),
                        StaffID = c.Int(nullable: false),
                        DatePosted = c.DateTime(nullable: false),
                        ExpiryDate = c.DateTime(nullable: false),
                        Priority = c.String(nullable: false),
                        TargetAudience = c.String(nullable: false),
                        ResidenceID = c.Int(),
                    })
                .PrimaryKey(t => t.AnnouncementID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID)
                .ForeignKey("dbo.Staffs", t => t.StaffID, cascadeDelete: true)
                .Index(t => t.StaffID)
                .Index(t => t.ResidenceID);
            
            CreateTable(
                "dbo.Staffs",
                c => new
                    {
                        StaffID = c.Int(nullable: false, identity: true),
                        StaffNumber = c.String(nullable: false),
                        FirstName = c.String(nullable: false),
                        LastName = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        Password = c.String(nullable: false),
                        PhoneNumber = c.String(nullable: false, maxLength: 15),
                        Role = c.String(nullable: false),
                        DateRegistered = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        ResidenceID = c.Int(),
                    })
                .PrimaryKey(t => t.StaffID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID)
                .Index(t => t.ResidenceID);
            
            CreateTable(
                "dbo.Complaints",
                c => new
                    {
                        ComplaintId = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(),
                        Subject = c.String(nullable: false, maxLength: 120),
                        Category = c.String(nullable: false, maxLength: 200),
                        Description = c.String(nullable: false, maxLength: 1000),
                        DateSubmitted = c.DateTime(nullable: false),
                        Status = c.String(nullable: false, maxLength: 30),
                        ManagerFeedback = c.String(maxLength: 500),
                        LastUpdated = c.DateTime(),
                        DateResolved = c.DateTime(),
                        ReviewedByStaffID = c.Int(),
                    })
                .PrimaryKey(t => t.ComplaintId)
                .ForeignKey("dbo.Staffs", t => t.ReviewedByStaffID)
                .ForeignKey("dbo.Students", t => t.StudentID)
                .Index(t => t.StudentID)
                .Index(t => t.ReviewedByStaffID);
            
            CreateTable(
                "dbo.Maintenances",
                c => new
                    {
                        MaintenanceID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        RoomID = c.Int(),
                        IssueType = c.Int(nullable: false),
                        IssueDescription = c.String(nullable: false, maxLength: 500),
                        DateReported = c.DateTime(nullable: false),
                        RoomNumber = c.String(nullable: false, maxLength: 50),
                        Status = c.String(nullable: false),
                        DateResolved = c.DateTime(),
                        IsConfirmedByStudent = c.Boolean(nullable: false),
                        ImagePath = c.String(),
                        StaffID = c.Int(),
                        TechnicianID = c.Int(),
                        CompletionImage = c.String(),
                    })
                .PrimaryKey(t => t.MaintenanceID)
                .ForeignKey("dbo.Rooms", t => t.RoomID)
                .ForeignKey("dbo.Staffs", t => t.StaffID)
                .ForeignKey("dbo.Students", t => t.StudentID, cascadeDelete: true)
                .ForeignKey("dbo.Technicians", t => t.TechnicianID)
                .Index(t => t.StudentID)
                .Index(t => t.RoomID)
                .Index(t => t.StaffID)
                .Index(t => t.TechnicianID);
            
            CreateTable(
                "dbo.Technicians",
                c => new
                    {
                        TechnicianID = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false, maxLength: 100),
                        TechnicianType = c.String(nullable: false, maxLength: 50),
                        PhoneNumber = c.String(nullable: false, maxLength: 10),
                        Email = c.String(maxLength: 100),
                        Password = c.String(),
                        AvailabilityStatus = c.Boolean(nullable: false),
                        DateAdded = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TechnicianID);
            
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        NotificationID = c.Int(nullable: false, identity: true),
                        UserID = c.Int(nullable: false),
                        UserType = c.String(nullable: false, maxLength: 50),
                        Title = c.String(nullable: false, maxLength: 100),
                        Message = c.String(nullable: false, maxLength: 500),
                        NotificationType = c.String(nullable: false, maxLength: 50),
                        RelatedID = c.Int(),
                        RelatedType = c.String(maxLength: 50),
                        IsRead = c.Boolean(),
                        DateCreated = c.DateTime(nullable: false),
                        ExpiryDate = c.DateTime(),
                    })
                .PrimaryKey(t => t.NotificationID);
            
            CreateTable(
                "dbo.ResidenceApplications",
                c => new
                    {
                        ApplicationID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        Faculty = c.String(),
                        Gender = c.String(),
                        Level = c.String(),
                        ProofDocument = c.String(),
                        Status = c.String(),
                        AdminFeedback = c.String(),
                        ApplicationDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ApplicationID)
                .ForeignKey("dbo.Students", t => t.StudentID, cascadeDelete: true)
                .Index(t => t.StudentID);
            
            CreateTable(
                "dbo.ResidenceChangeRequests",
                c => new
                    {
                        RequestID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        CurrentResidenceID = c.Int(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 1000),
                        DocumentPath = c.String(),
                        Status = c.String(nullable: false),
                        AdminFeedback = c.String(),
                        DateRequested = c.DateTime(nullable: false),
                        DateReviewed = c.DateTime(),
                        ReviewedByStaffID = c.Int(),
                    })
                .PrimaryKey(t => t.RequestID)
                .ForeignKey("dbo.Residences", t => t.CurrentResidenceID, cascadeDelete: true)
                .ForeignKey("dbo.Staffs", t => t.ReviewedByStaffID)
                .ForeignKey("dbo.Students", t => t.StudentID, cascadeDelete: true)
                .Index(t => t.StudentID)
                .Index(t => t.CurrentResidenceID)
                .Index(t => t.ReviewedByStaffID);
            
            CreateTable(
                "dbo.ResidenceCheckIns",
                c => new
                    {
                        CheckInID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        ResidenceID = c.Int(nullable: false),
                        QRToken = c.String(nullable: false),
                        HasCheckedIn = c.Boolean(nullable: false),
                        CheckInTime = c.DateTime(),
                        HasCheckedOut = c.Boolean(nullable: false),
                        CheckOutTime = c.DateTime(),
                        TokenGeneratedAt = c.DateTime(nullable: false),
                        GeneratedByStaffID = c.Int(),
                    })
                .PrimaryKey(t => t.CheckInID)
                .ForeignKey("dbo.Staffs", t => t.GeneratedByStaffID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentID, cascadeDelete: true)
                .Index(t => t.StudentID)
                .Index(t => t.ResidenceID)
                .Index(t => t.GeneratedByStaffID);
            
            CreateTable(
                "dbo.RoomChangeRequests",
                c => new
                    {
                        RequestID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        CurrentRoomID = c.Int(nullable: false),
                        RequestedRoomID = c.Int(),
                        Reason = c.String(nullable: false, maxLength: 1000),
                        DocumentPath = c.String(),
                        Status = c.String(nullable: false),
                        AdminFeedback = c.String(maxLength: 500),
                        DateRequested = c.DateTime(nullable: false),
                        DateReviewed = c.DateTime(),
                        ReviewedByStaffID = c.Int(),
                        CurrentRoom_RoomID = c.Int(),
                        RequestedRoom_RoomID = c.Int(),
                    })
                .PrimaryKey(t => t.RequestID)
                .ForeignKey("dbo.Rooms", t => t.CurrentRoom_RoomID)
                .ForeignKey("dbo.Rooms", t => t.RequestedRoom_RoomID)
                .ForeignKey("dbo.Staffs", t => t.ReviewedByStaffID)
                .ForeignKey("dbo.Students", t => t.StudentID, cascadeDelete: true)
                .Index(t => t.StudentID)
                .Index(t => t.ReviewedByStaffID)
                .Index(t => t.CurrentRoom_RoomID)
                .Index(t => t.RequestedRoom_RoomID);
            
            CreateTable(
                "dbo.StudentAnnouncementViews",
                c => new
                    {
                        ViewID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        AnnouncementID = c.Int(nullable: false),
                        DateViewed = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ViewID)
                .ForeignKey("dbo.Announcements", t => t.AnnouncementID, cascadeDelete: true)
                .ForeignKey("dbo.Students", t => t.StudentID, cascadeDelete: true)
                .Index(t => t.StudentID)
                .Index(t => t.AnnouncementID);
            
            CreateTable(
                "dbo.Visitors",
                c => new
                    {
                        VisitorID = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false),
                        IDNumber = c.String(nullable: false),
                        DocumentType = c.String(nullable: false),
                        CheckInTime = c.DateTime(nullable: false),
                        EntryTime = c.DateTime(),
                        CheckOutTime = c.DateTime(),
                        StudentID = c.Int(nullable: false),
                        ResidenceID = c.Int(nullable: false),
                        QRCode = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        CurfewAlertSent = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.VisitorID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StudentAnnouncementViews", "StudentID", "dbo.Students");
            DropForeignKey("dbo.StudentAnnouncementViews", "AnnouncementID", "dbo.Announcements");
            DropForeignKey("dbo.RoomChangeRequests", "StudentID", "dbo.Students");
            DropForeignKey("dbo.RoomChangeRequests", "ReviewedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.RoomChangeRequests", "RequestedRoom_RoomID", "dbo.Rooms");
            DropForeignKey("dbo.RoomChangeRequests", "CurrentRoom_RoomID", "dbo.Rooms");
            DropForeignKey("dbo.ResidenceCheckIns", "StudentID", "dbo.Students");
            DropForeignKey("dbo.ResidenceCheckIns", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.ResidenceCheckIns", "GeneratedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.ResidenceChangeRequests", "StudentID", "dbo.Students");
            DropForeignKey("dbo.ResidenceChangeRequests", "ReviewedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.ResidenceChangeRequests", "CurrentResidenceID", "dbo.Residences");
            DropForeignKey("dbo.ResidenceApplications", "StudentID", "dbo.Students");
            DropForeignKey("dbo.Maintenances", "TechnicianID", "dbo.Technicians");
            DropForeignKey("dbo.Maintenances", "StudentID", "dbo.Students");
            DropForeignKey("dbo.Maintenances", "StaffID", "dbo.Staffs");
            DropForeignKey("dbo.Maintenances", "RoomID", "dbo.Rooms");
            DropForeignKey("dbo.Complaints", "StudentID", "dbo.Students");
            DropForeignKey("dbo.Complaints", "ReviewedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.Announcements", "StaffID", "dbo.Staffs");
            DropForeignKey("dbo.Staffs", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.Announcements", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.Allocations", "StudentID", "dbo.Students");
            DropForeignKey("dbo.Allocations", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.Students", "RoomID", "dbo.Rooms");
            DropForeignKey("dbo.Students", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.Rooms", "ResidenceID", "dbo.Residences");
            DropIndex("dbo.StudentAnnouncementViews", new[] { "AnnouncementID" });
            DropIndex("dbo.StudentAnnouncementViews", new[] { "StudentID" });
            DropIndex("dbo.RoomChangeRequests", new[] { "RequestedRoom_RoomID" });
            DropIndex("dbo.RoomChangeRequests", new[] { "CurrentRoom_RoomID" });
            DropIndex("dbo.RoomChangeRequests", new[] { "ReviewedByStaffID" });
            DropIndex("dbo.RoomChangeRequests", new[] { "StudentID" });
            DropIndex("dbo.ResidenceCheckIns", new[] { "GeneratedByStaffID" });
            DropIndex("dbo.ResidenceCheckIns", new[] { "ResidenceID" });
            DropIndex("dbo.ResidenceCheckIns", new[] { "StudentID" });
            DropIndex("dbo.ResidenceChangeRequests", new[] { "ReviewedByStaffID" });
            DropIndex("dbo.ResidenceChangeRequests", new[] { "CurrentResidenceID" });
            DropIndex("dbo.ResidenceChangeRequests", new[] { "StudentID" });
            DropIndex("dbo.ResidenceApplications", new[] { "StudentID" });
            DropIndex("dbo.Maintenances", new[] { "TechnicianID" });
            DropIndex("dbo.Maintenances", new[] { "StaffID" });
            DropIndex("dbo.Maintenances", new[] { "RoomID" });
            DropIndex("dbo.Maintenances", new[] { "StudentID" });
            DropIndex("dbo.Complaints", new[] { "ReviewedByStaffID" });
            DropIndex("dbo.Complaints", new[] { "StudentID" });
            DropIndex("dbo.Staffs", new[] { "ResidenceID" });
            DropIndex("dbo.Announcements", new[] { "ResidenceID" });
            DropIndex("dbo.Announcements", new[] { "StaffID" });
            DropIndex("dbo.Students", new[] { "RoomID" });
            DropIndex("dbo.Students", new[] { "ResidenceID" });
            DropIndex("dbo.Rooms", new[] { "ResidenceID" });
            DropIndex("dbo.Allocations", new[] { "ResidenceID" });
            DropIndex("dbo.Allocations", new[] { "StudentID" });
            DropTable("dbo.Visitors");
            DropTable("dbo.StudentAnnouncementViews");
            DropTable("dbo.RoomChangeRequests");
            DropTable("dbo.ResidenceCheckIns");
            DropTable("dbo.ResidenceChangeRequests");
            DropTable("dbo.ResidenceApplications");
            DropTable("dbo.Notifications");
            DropTable("dbo.Technicians");
            DropTable("dbo.Maintenances");
            DropTable("dbo.Complaints");
            DropTable("dbo.Staffs");
            DropTable("dbo.Announcements");
            DropTable("dbo.Students");
            DropTable("dbo.Rooms");
            DropTable("dbo.Residences");
            DropTable("dbo.Allocations");
        }
    }
}
