namespace DUTResManagementSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class Semester2UseCases : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Maintenances", "Priority", c => c.String(maxLength: 20, defaultValue: "Normal"));
            AddColumn("dbo.Maintenances", "TargetResponseBy", c => c.DateTime());
            AddColumn("dbo.Maintenances", "EscalatedAt", c => c.DateTime());
            AddColumn("dbo.Maintenances", "EscalationReason", c => c.String(maxLength: 500));
            AddColumn("dbo.Maintenances", "IsSafetyCritical", c => c.Boolean(nullable: false, defaultValue: false));

            AddColumn("dbo.Complaints", "Priority", c => c.String(maxLength: 20, defaultValue: "Normal"));
            AddColumn("dbo.Complaints", "TargetResolutionBy", c => c.DateTime());
            AddColumn("dbo.Complaints", "EscalatedAt", c => c.DateTime());
            AddColumn("dbo.Complaints", "EscalationReason", c => c.String(maxLength: 500));

            AddColumn("dbo.Visitors", "IdentityVerifiedAt", c => c.DateTime());
            AddColumn("dbo.Visitors", "IdentityVerified", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("dbo.Visitors", "OverstayAlertSentAt", c => c.DateTime());
            AddColumn("dbo.Visitors", "IsOverstayFlagged", c => c.Boolean(nullable: false, defaultValue: false));

            CreateTable(
                "dbo.RoomInspections",
                c => new
                    {
                        RoomInspectionID = c.Int(nullable: false, identity: true),
                        RoomID = c.Int(nullable: false),
                        StudentID = c.Int(),
                        InspectionType = c.String(nullable: false, maxLength: 20),
                        ConditionStatus = c.String(nullable: false, maxLength: 30),
                        Notes = c.String(maxLength: 1000),
                        RequiresMaintenance = c.Boolean(nullable: false),
                        BlocksAllocation = c.Boolean(nullable: false),
                        InspectionDate = c.DateTime(nullable: false),
                        InspectedByStaffID = c.Int(),
                    })
                .PrimaryKey(t => t.RoomInspectionID)
                .ForeignKey("dbo.Rooms", t => t.RoomID, cascadeDelete: false)
                .ForeignKey("dbo.Students", t => t.StudentID)
                .ForeignKey("dbo.Staffs", t => t.InspectedByStaffID)
                .Index(t => t.RoomID)
                .Index(t => t.StudentID)
                .Index(t => t.InspectedByStaffID);

            CreateTable(
                "dbo.EmergencyRollCalls",
                c => new
                    {
                        EmergencyRollCallID = c.Int(nullable: false, identity: true),
                        ResidenceID = c.Int(nullable: false),
                        IncidentTitle = c.String(nullable: false, maxLength: 120),
                        IncidentNotes = c.String(maxLength: 1000),
                        Status = c.String(nullable: false, maxLength: 30),
                        StartedAt = c.DateTime(nullable: false),
                        ClosedAt = c.DateTime(),
                        StartedByStaffID = c.Int(),
                    })
                .PrimaryKey(t => t.EmergencyRollCallID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID, cascadeDelete: false)
                .ForeignKey("dbo.Staffs", t => t.StartedByStaffID)
                .Index(t => t.ResidenceID)
                .Index(t => t.StartedByStaffID);

            CreateTable(
                "dbo.EmergencyRollCallPersons",
                c => new
                    {
                        EmergencyRollCallPersonID = c.Int(nullable: false, identity: true),
                        EmergencyRollCallID = c.Int(nullable: false),
                        PersonType = c.String(nullable: false, maxLength: 20),
                        StudentID = c.Int(),
                        VisitorID = c.Int(),
                        DisplayName = c.String(nullable: false, maxLength: 120),
                        RoomNumber = c.String(maxLength: 80),
                        SafetyStatus = c.String(nullable: false, maxLength: 20),
                        Notes = c.String(maxLength: 500),
                        MarkedAt = c.DateTime(),
                        MarkedByStaffID = c.Int(),
                    })
                .PrimaryKey(t => t.EmergencyRollCallPersonID)
                .ForeignKey("dbo.EmergencyRollCalls", t => t.EmergencyRollCallID, cascadeDelete: true)
                .Index(t => t.EmergencyRollCallID);
        }

        public override void Down()
        {
            DropForeignKey("dbo.EmergencyRollCallPersons", "EmergencyRollCallID", "dbo.EmergencyRollCalls");
            DropForeignKey("dbo.EmergencyRollCalls", "StartedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.EmergencyRollCalls", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.RoomInspections", "InspectedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.RoomInspections", "StudentID", "dbo.Students");
            DropForeignKey("dbo.RoomInspections", "RoomID", "dbo.Rooms");
            DropIndex("dbo.EmergencyRollCallPersons", new[] { "EmergencyRollCallID" });
            DropIndex("dbo.EmergencyRollCalls", new[] { "StartedByStaffID" });
            DropIndex("dbo.EmergencyRollCalls", new[] { "ResidenceID" });
            DropIndex("dbo.RoomInspections", new[] { "InspectedByStaffID" });
            DropIndex("dbo.RoomInspections", new[] { "StudentID" });
            DropIndex("dbo.RoomInspections", new[] { "RoomID" });
            DropTable("dbo.EmergencyRollCallPersons");
            DropTable("dbo.EmergencyRollCalls");
            DropTable("dbo.RoomInspections");
            DropColumn("dbo.Visitors", "IsOverstayFlagged");
            DropColumn("dbo.Visitors", "OverstayAlertSentAt");
            DropColumn("dbo.Visitors", "IdentityVerified");
            DropColumn("dbo.Visitors", "IdentityVerifiedAt");
            DropColumn("dbo.Complaints", "EscalationReason");
            DropColumn("dbo.Complaints", "EscalatedAt");
            DropColumn("dbo.Complaints", "TargetResolutionBy");
            DropColumn("dbo.Complaints", "Priority");
            DropColumn("dbo.Maintenances", "IsSafetyCritical");
            DropColumn("dbo.Maintenances", "EscalationReason");
            DropColumn("dbo.Maintenances", "EscalatedAt");
            DropColumn("dbo.Maintenances", "TargetResponseBy");
            DropColumn("dbo.Maintenances", "Priority");
        }
    }
}
