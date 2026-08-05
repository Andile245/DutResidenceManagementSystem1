namespace DUTResManagementSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ElectionManagementSchema : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CommitteeAppointments",
                c => new
                    {
                        CommitteeAppointmentID = c.Int(nullable: false, identity: true),
                        ResidenceElectionID = c.Int(nullable: false),
                        ElectionPositionID = c.Int(nullable: false),
                        StudentID = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        AppointedAt = c.DateTime(nullable: false),
                        EndedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.CommitteeAppointmentID)
                .ForeignKey("dbo.Students", t => t.StudentID)
                .Index(t => t.StudentID);
            
            CreateTable(
                "dbo.ElectionAuditLogs",
                c => new
                    {
                        ElectionAuditLogID = c.Int(nullable: false, identity: true),
                        ResidenceElectionID = c.Int(),
                        EventType = c.String(nullable: false, maxLength: 80),
                        Detail = c.String(nullable: false, maxLength: 1000),
                        ActorType = c.String(maxLength: 30),
                        ActorID = c.Int(),
                        OccurredAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ElectionAuditLogID);
            
            CreateTable(
                "dbo.ElectionNominations",
                c => new
                    {
                        ElectionNominationID = c.Int(nullable: false, identity: true),
                        ResidenceElectionID = c.Int(nullable: false),
                        ElectionPositionID = c.Int(nullable: false),
                        StudentID = c.Int(nullable: false),
                        EligibilityRecommendation = c.String(nullable: false, maxLength: 30),
                        EligibilityReason = c.String(maxLength: 1000),
                        Status = c.String(nullable: false, maxLength: 30),
                        Manifesto = c.String(nullable: false, maxLength: 1500),
                        Motivation = c.String(nullable: false, maxLength: 1000),
                        ProfilePhotoPath = c.String(maxLength: 300),
                        ReviewNote = c.String(maxLength: 500),
                        ReviewedByStaffID = c.Int(),
                        SubmittedAt = c.DateTime(nullable: false),
                        ReviewedAt = c.DateTime(),
                        IsWinner = c.Boolean(nullable: false),
                        VoteCount = c.Int(),
                    })
                .PrimaryKey(t => t.ElectionNominationID)
                .ForeignKey("dbo.ResidenceElections", t => t.ResidenceElectionID)
                .ForeignKey("dbo.ElectionPositions", t => t.ElectionPositionID)
                .ForeignKey("dbo.Students", t => t.StudentID)
                .Index(t => new { t.ResidenceElectionID, t.StudentID }, unique: true)
                .Index(t => t.ElectionPositionID);
            
            CreateTable(
                "dbo.ResidenceElections",
                c => new
                    {
                        ResidenceElectionID = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 120),
                        ResidenceID = c.Int(nullable: false),
                        NominationOpensAt = c.DateTime(nullable: false),
                        NominationClosesAt = c.DateTime(nullable: false),
                        CampaignOpensAt = c.DateTime(nullable: false),
                        VotingOpensAt = c.DateTime(nullable: false),
                        VotingClosesAt = c.DateTime(nullable: false),
                        Status = c.String(nullable: false, maxLength: 30),
                        PreventSelfVote = c.Boolean(nullable: false),
                        CreatedByStaffID = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        ResultsPublishedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.ResidenceElectionID)
                .ForeignKey("dbo.Residences", t => t.ResidenceID)
                .Index(t => t.ResidenceID);
            
            CreateTable(
                "dbo.ElectionPositions",
                c => new
                    {
                        ElectionPositionID = c.Int(nullable: false, identity: true),
                        ResidenceElectionID = c.Int(nullable: false),
                        Name = c.String(nullable: false, maxLength: 80),
                        Seats = c.Int(nullable: false),
                        DisplayOrder = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ElectionPositionID)
                .ForeignKey("dbo.ResidenceElections", t => t.ResidenceElectionID)
                .Index(t => t.ResidenceElectionID);
            
            CreateTable(
                "dbo.ElectionParticipations",
                c => new
                    {
                        ElectionParticipationID = c.Int(nullable: false, identity: true),
                        ResidenceElectionID = c.Int(nullable: false),
                        ElectionPositionID = c.Int(nullable: false),
                        StudentID = c.Int(nullable: false),
                        CastAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ElectionParticipationID)
                .Index(t => new { t.ResidenceElectionID, t.ElectionPositionID, t.StudentID }, unique: true);
            
            CreateTable(
                "dbo.ElectionVotes",
                c => new
                    {
                        ElectionVoteID = c.Int(nullable: false, identity: true),
                        ResidenceElectionID = c.Int(nullable: false),
                        ElectionPositionID = c.Int(nullable: false),
                        ElectionNominationID = c.Int(nullable: false),
                        CastAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ElectionVoteID)
                .ForeignKey("dbo.ElectionNominations", t => t.ElectionNominationID)
                .Index(t => new { t.ResidenceElectionID, t.ElectionPositionID, t.ElectionNominationID });
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ElectionVotes", "ElectionNominationID", "dbo.ElectionNominations");
            DropForeignKey("dbo.ElectionNominations", "StudentID", "dbo.Students");
            DropForeignKey("dbo.ElectionNominations", "ElectionPositionID", "dbo.ElectionPositions");
            DropForeignKey("dbo.ElectionNominations", "ResidenceElectionID", "dbo.ResidenceElections");
            DropForeignKey("dbo.ResidenceElections", "ResidenceID", "dbo.Residences");
            DropForeignKey("dbo.ElectionPositions", "ResidenceElectionID", "dbo.ResidenceElections");
            DropForeignKey("dbo.CommitteeAppointments", "StudentID", "dbo.Students");
            DropIndex("dbo.ElectionVotes", new[] { "ResidenceElectionID", "ElectionPositionID", "ElectionNominationID" });
            DropIndex("dbo.ElectionParticipations", new[] { "ResidenceElectionID", "ElectionPositionID", "StudentID" });
            DropIndex("dbo.ElectionPositions", new[] { "ResidenceElectionID" });
            DropIndex("dbo.ResidenceElections", new[] { "ResidenceID" });
            DropIndex("dbo.ElectionNominations", new[] { "ElectionPositionID" });
            DropIndex("dbo.ElectionNominations", new[] { "ResidenceElectionID", "StudentID" });
            DropIndex("dbo.CommitteeAppointments", new[] { "StudentID" });
            DropTable("dbo.ElectionVotes");
            DropTable("dbo.ElectionParticipations");
            DropTable("dbo.ElectionPositions");
            DropTable("dbo.ResidenceElections");
            DropTable("dbo.ElectionNominations");
            DropTable("dbo.ElectionAuditLogs");
            DropTable("dbo.CommitteeAppointments");
        }
    }
}
