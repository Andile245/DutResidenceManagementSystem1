using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;

namespace DUTResManagementSystem.ViewModels
{
    public class Semester2OperationsViewModel
    {
        public Staff Staff { get; set; }
        public string ResidenceName { get; set; }
        public List<Maintenance> EscalatedMaintenance { get; set; } = new List<Maintenance>();
        public List<Complaint> EscalatedComplaints { get; set; } = new List<Complaint>();
        public List<Visitor> VisitorExceptions { get; set; } = new List<Visitor>();
        public List<RoomInspection> RecentInspections { get; set; } = new List<RoomInspection>();
        public List<EmergencyRollCall> OpenRollCalls { get; set; } = new List<EmergencyRollCall>();
        public List<OccupancyForecastItem> OccupancyForecast { get; set; } = new List<OccupancyForecastItem>();
    }

    public class OccupancyForecastItem
    {
        public int ResidenceID { get; set; }
        public string ResidenceName { get; set; }
        public int Capacity { get; set; }
        public int Occupied { get; set; }
        public int PendingApplications { get; set; }
        public int AvailableSpaces { get; set; }
        public decimal OccupancyRate { get; set; }
        public string RiskLevel { get; set; }
        public string Recommendation { get; set; }
    }

    public class RollCallPersonViewModel
    {
        public int EmergencyRollCallPersonID { get; set; }
        public string PersonType { get; set; }
        public string DisplayName { get; set; }
        public string RoomNumber { get; set; }
        public string SafetyStatus { get; set; }
        public string Notes { get; set; }
    }

    public class EmergencyRollCallViewModel
    {
        public EmergencyRollCall RollCall { get; set; }
        public List<RollCallPersonViewModel> People { get; set; } = new List<RollCallPersonViewModel>();
    }
}
