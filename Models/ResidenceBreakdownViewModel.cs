using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.Models
{
    public class ResidenceBreakdownViewModel
    {
        public int ResidenceID { get; set; }
        public string Name { get; set; }
        public int Capacity { get; set; }
        public int Occupied { get; set; }
        public int Available { get; set; }
        public string GenderPolicy { get; set; }
        public string Faculty { get; set; }
    }
}