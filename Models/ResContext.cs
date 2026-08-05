using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using DUTResManagementSystem.Models;


namespace DUTResManagementSystem.Models
{
    public class ResContext : DbContext
    {
        public ResContext() : base("StudentManagementContext")
        {
        }

        public DbSet<Student> Students { get; set; }

        public DbSet<Residence> Residences { get; set; }

        public DbSet<ResidenceApplication> ResidenceApplications { get; set; }

        public DbSet<Allocation> Allocations { get; set; }

        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<Maintenance> Maintenances { get; set; }
        public DbSet<Room> Rooms { get; set; }

        public DbSet<Notification> Notifications { get; set; }
        public DbSet<StudentAnnouncementView> StudentAnnouncementViews { get; set; }
        public DbSet<RoomChangeRequest> RoomChangeRequests { get; set; }

        
        public DbSet<ResidenceCheckIn> ResidenceCheckIns { get; set; }
        public DbSet<ResidenceChangeRequest> ResidenceChangeRequests { get; set; }
        public DbSet<Technician> Technicians { get; set; }
        public DbSet<Visitor> Visitors { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<StudentConductRecord> StudentConductRecords { get; set; }
        public DbSet<RoomInspection> RoomInspections { get; set; }
        public DbSet<EmergencyRollCall> EmergencyRollCalls { get; set; }
        public DbSet<EmergencyRollCallPerson> EmergencyRollCallPeople { get; set; }
        public DbSet<ResidenceElection> ResidenceElections { get; set; }
        public DbSet<ElectionPosition> ElectionPositions { get; set; }
        public DbSet<ElectionNomination> ElectionNominations { get; set; }
        public DbSet<ElectionParticipation> ElectionParticipations { get; set; }
        public DbSet<ElectionVote> ElectionVotes { get; set; }
        public DbSet<CommitteeAppointment> CommitteeAppointments { get; set; }
        public DbSet<ElectionAuditLog> ElectionAuditLogs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ElectionParticipation>()
                .HasIndex(p => new { p.ResidenceElectionID, p.ElectionPositionID, p.StudentID })
                .IsUnique();
            modelBuilder.Entity<ElectionVote>()
                .HasIndex(v => new { v.ResidenceElectionID, v.ElectionPositionID, v.ElectionNominationID });
            modelBuilder.Entity<ElectionNomination>()
                .HasIndex(n => new { n.ResidenceElectionID, n.StudentID })
                .IsUnique();
            // Election data is historical/auditable. Never cascade-delete it through
            // a residence, position, student, or nomination relationship.
            modelBuilder.Entity<ResidenceElection>()
                .HasRequired(e => e.Residence).WithMany().HasForeignKey(e => e.ResidenceID).WillCascadeOnDelete(false);
            modelBuilder.Entity<ElectionPosition>()
                .HasRequired(p => p.Election).WithMany(e => e.Positions).HasForeignKey(p => p.ResidenceElectionID).WillCascadeOnDelete(false);
            modelBuilder.Entity<ElectionNomination>()
                .HasRequired(n => n.Election).WithMany().HasForeignKey(n => n.ResidenceElectionID).WillCascadeOnDelete(false);
            modelBuilder.Entity<ElectionNomination>()
                .HasRequired(n => n.Position).WithMany(p => p.Nominations).HasForeignKey(n => n.ElectionPositionID).WillCascadeOnDelete(false);
            modelBuilder.Entity<ElectionNomination>()
                .HasRequired(n => n.Student).WithMany().HasForeignKey(n => n.StudentID).WillCascadeOnDelete(false);
            modelBuilder.Entity<ElectionVote>()
                .HasRequired(v => v.Nomination).WithMany().HasForeignKey(v => v.ElectionNominationID).WillCascadeOnDelete(false);
            modelBuilder.Entity<CommitteeAppointment>()
                .HasRequired(a => a.Student).WithMany().HasForeignKey(a => a.StudentID).WillCascadeOnDelete(false);
            modelBuilder.Entity<Complaint>()
                .HasOptional(c => c.ReportedStudent).WithMany().HasForeignKey(c => c.ReportedStudentID).WillCascadeOnDelete(false);
            modelBuilder.Entity<StudentConductRecord>()
                .HasRequired(r => r.Student).WithMany().HasForeignKey(r => r.StudentID).WillCascadeOnDelete(false);
            modelBuilder.Entity<StudentConductRecord>()
                .HasRequired(r => r.Complaint).WithMany().HasForeignKey(r => r.ComplaintId).WillCascadeOnDelete(false);
            modelBuilder.Entity<StudentConductRecord>()
                .HasRequired(r => r.IssuedByStaff).WithMany().HasForeignKey(r => r.IssuedByStaffID).WillCascadeOnDelete(false);
            base.OnModelCreating(modelBuilder);
        }
    }
}
