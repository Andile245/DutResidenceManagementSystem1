namespace DUTResManagementSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ImprovedElectionWorkflow : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ElectionNominations", "CampaignImagePath", c => c.String(maxLength: 300));
            AddColumn("dbo.ElectionNominations", "AcceptedElectionRules", c => c.Boolean(nullable: false));
            AddColumn("dbo.ElectionNominations", "WithdrawnAt", c => c.DateTime());
            AddColumn("dbo.ElectionNominations", "WithdrawalReason", c => c.String(maxLength: 500));
            AddColumn("dbo.ResidenceElections", "ResultsPublicationAt", c => c.DateTime(nullable: false));
            AddColumn("dbo.ResidenceElections", "MinimumTurnoutPercentage", c => c.Decimal(precision: 18, scale: 2));
            AddColumn("dbo.ResidenceElections", "ArchivedAt", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.ResidenceElections", "ArchivedAt");
            DropColumn("dbo.ResidenceElections", "MinimumTurnoutPercentage");
            DropColumn("dbo.ResidenceElections", "ResultsPublicationAt");
            DropColumn("dbo.ElectionNominations", "WithdrawalReason");
            DropColumn("dbo.ElectionNominations", "WithdrawnAt");
            DropColumn("dbo.ElectionNominations", "AcceptedElectionRules");
            DropColumn("dbo.ElectionNominations", "CampaignImagePath");
        }
    }
}
