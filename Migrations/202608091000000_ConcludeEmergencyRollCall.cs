namespace DUTResManagementSystem.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class ConcludeEmergencyRollCall : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.EmergencyRollCalls", "ConclusionNotes", c => c.String(maxLength: 1000));
            AddColumn("dbo.EmergencyRollCalls", "OutstandingPeopleCount", c => c.Int(nullable: false, defaultValue: 0));
            AddColumn("dbo.EmergencyRollCalls", "ConcludedByStaffID", c => c.Int());
            CreateIndex("dbo.EmergencyRollCalls", "ConcludedByStaffID");
            AddForeignKey("dbo.EmergencyRollCalls", "ConcludedByStaffID", "dbo.Staffs", "StaffID");
        }

        public override void Down()
        {
            DropForeignKey("dbo.EmergencyRollCalls", "ConcludedByStaffID", "dbo.Staffs");
            DropIndex("dbo.EmergencyRollCalls", new[] { "ConcludedByStaffID" });
            DropColumn("dbo.EmergencyRollCalls", "ConcludedByStaffID");
            DropColumn("dbo.EmergencyRollCalls", "OutstandingPeopleCount");
            DropColumn("dbo.EmergencyRollCalls", "ConclusionNotes");
        }
    }
}
