namespace DUTResManagementSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class ActivateHouseCommittee : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ResidenceElections", "ResultsValidatedAt", c => c.DateTime());
            AddColumn("dbo.ResidenceElections", "ResultsValidatedByStaffID", c => c.Int());
            AddColumn("dbo.ResidenceElections", "HasUnresolvedDisputes", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("dbo.CommitteeAppointments", "ResidenceID", c => c.Int(nullable: false, defaultValue: 0));
            Sql("UPDATE ca SET ResidenceID = e.ResidenceID FROM dbo.CommitteeAppointments ca INNER JOIN dbo.ResidenceElections e ON e.ResidenceElectionID = ca.ResidenceElectionID");
            AddColumn("dbo.CommitteeAppointments", "Term", c => c.String(nullable: false, maxLength: 160, defaultValue: "Legacy committee term"));
            AddColumn("dbo.CommitteeAppointments", "MemberStatus", c => c.String(nullable: false, maxLength: 60, defaultValue: "Active House Committee Member"));
            AddColumn("dbo.CommitteeAppointments", "ActivatedAt", c => c.DateTime(nullable: false, defaultValueSql: "GETDATE()"));
        }

        public override void Down()
        {
            DropColumn("dbo.CommitteeAppointments", "ActivatedAt");
            DropColumn("dbo.CommitteeAppointments", "MemberStatus");
            DropColumn("dbo.CommitteeAppointments", "Term");
            DropColumn("dbo.CommitteeAppointments", "ResidenceID");
            DropColumn("dbo.ResidenceElections", "ResultsValidatedByStaffID");
            DropColumn("dbo.ResidenceElections", "HasUnresolvedDisputes");
            DropColumn("dbo.ResidenceElections", "ResultsValidatedAt");
        }
    }
}
