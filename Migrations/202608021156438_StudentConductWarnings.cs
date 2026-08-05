namespace DUTResManagementSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class StudentConductWarnings : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.StudentConductRecords",
                c => new
                    {
                        StudentConductRecordID = c.Int(nullable: false, identity: true),
                        StudentID = c.Int(nullable: false),
                        ComplaintId = c.Int(nullable: false),
                        Severity = c.String(nullable: false, maxLength: 20),
                        Reason = c.String(nullable: false, maxLength: 500),
                        IsActive = c.Boolean(nullable: false),
                        IssuedAt = c.DateTime(nullable: false),
                        IssuedByStaffID = c.Int(nullable: false),
                        ResolvedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.StudentConductRecordID)
                .ForeignKey("dbo.Complaints", t => t.ComplaintId)
                .ForeignKey("dbo.Staffs", t => t.IssuedByStaffID)
                .ForeignKey("dbo.Students", t => t.StudentID)
                .Index(t => t.StudentID)
                .Index(t => t.ComplaintId)
                .Index(t => t.IssuedByStaffID);
            
            AddColumn("dbo.Complaints", "ReportedStudentID", c => c.Int());
            AddColumn("dbo.Complaints", "WarningIssued", c => c.Boolean(nullable: false));
            AddColumn("dbo.Complaints", "WarningSeverity", c => c.String(maxLength: 20));
            AddColumn("dbo.Complaints", "WarningReason", c => c.String(maxLength: 500));
            AddColumn("dbo.Complaints", "WarningIssuedAt", c => c.DateTime());
            AddColumn("dbo.Complaints", "WarningIssuedByStaffID", c => c.Int());
            CreateIndex("dbo.Complaints", "ReportedStudentID");
            AddForeignKey("dbo.Complaints", "ReportedStudentID", "dbo.Students", "StudentID");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StudentConductRecords", "StudentID", "dbo.Students");
            DropForeignKey("dbo.StudentConductRecords", "IssuedByStaffID", "dbo.Staffs");
            DropForeignKey("dbo.StudentConductRecords", "ComplaintId", "dbo.Complaints");
            DropForeignKey("dbo.Complaints", "ReportedStudentID", "dbo.Students");
            DropIndex("dbo.StudentConductRecords", new[] { "IssuedByStaffID" });
            DropIndex("dbo.StudentConductRecords", new[] { "ComplaintId" });
            DropIndex("dbo.StudentConductRecords", new[] { "StudentID" });
            DropIndex("dbo.Complaints", new[] { "ReportedStudentID" });
            DropColumn("dbo.Complaints", "WarningIssuedByStaffID");
            DropColumn("dbo.Complaints", "WarningIssuedAt");
            DropColumn("dbo.Complaints", "WarningReason");
            DropColumn("dbo.Complaints", "WarningSeverity");
            DropColumn("dbo.Complaints", "WarningIssued");
            DropColumn("dbo.Complaints", "ReportedStudentID");
            DropTable("dbo.StudentConductRecords");
        }
    }
}
