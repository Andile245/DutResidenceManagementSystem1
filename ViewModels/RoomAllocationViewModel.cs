using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace DUTResManagementSystem.ViewModels
{
    public class RoomAllocationViewModel
    {
        // Dropdown list of all residences
        public SelectList Residences { get; set; }

        // The residence currently being viewed (null if none selected yet)
        public Residence SelectedResidence { get; set; }

        // OLD - Keep for backward compatibility, but mark as obsolete
        public List<Student> UnallocatedStudents { get; set; }

        // NEW: Categorized unallocated students by check-in status
        public UnallocatedStudentsViewModel UnallocatedStudentsByStatus { get; set; }

        // NEW: Students who already have rooms (already allocated)
        public List<StudentWithRoomInfo> StudentsWithRooms { get; set; }

        // All rooms in the selected residence, each carrying their current occupants
        public List<RoomWithOccupants> Rooms { get; set; }

        public RoomAllocationViewModel()
        {
            UnallocatedStudents = new List<Student>();
            UnallocatedStudentsByStatus = new UnallocatedStudentsViewModel();
            StudentsWithRooms = new List<StudentWithRoomInfo>();
            Rooms = new List<RoomWithOccupants>();
        }
    }

    // NEW: Container for categorized unallocated students
    public class UnallocatedStudentsViewModel
    {
        public List<Student> CheckedIn { get; set; }
        public List<Student> NotCheckedIn { get; set; }
        public int CheckedInCount { get; set; }
        public int NotCheckedInCount { get; set; }

        public UnallocatedStudentsViewModel()
        {
            CheckedIn = new List<Student>();
            NotCheckedIn = new List<Student>();
        }
    }

    // NEW: ViewModel for students who already have rooms
    public class StudentWithRoomInfo
    {
        public Student Student { get; set; }
        public Room Room { get; set; }
        public string RoomNumber { get; set; }

        // ADDED: Check-in/out status properties
        public bool HasCheckedIn { get; set; }
        public bool HasCheckedOut { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        // Helper properties for display
        public string CheckStatusBadge
        {
            get
            {
                if (HasCheckedOut) return "secondary";
                if (HasCheckedIn) return "success";
                return "warning";
            }
        }

        public string CheckStatusText
        {
            get
            {
                if (HasCheckedOut) return "Checked Out";
                if (HasCheckedIn) return "Checked In";
                return "Not Checked In";
            }
        }

        public string CheckStatusIcon
        {
            get
            {
                if (HasCheckedOut) return "fa-sign-out-alt";
                if (HasCheckedIn) return "fa-check";
                return "fa-clock";
            }
        }
    }

    // Pairs a Room with the students currently living in it
    public class RoomWithOccupants
    {
        public Room Room { get; set; }
        public List<Student> Occupants { get; set; }

        public int CurrentOccupants => Occupants?.Count ?? 0;
        public int FreeSpaces => Room.Capacity - CurrentOccupants;
        public bool IsFull => FreeSpaces <= 0;

        public string BadgeColour =>
            IsFull ? "danger" :
            FreeSpaces == 1 ? "warning" : "success";

        public RoomWithOccupants()
        {
            Occupants = new List<Student>();
        }
    }
}