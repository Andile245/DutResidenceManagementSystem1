using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.Models
{
    public class ResidenceApplication
    {
        [Key]
        public int ApplicationID { get; set; }

        public int StudentID { get; set; }

        public string Faculty { get; set; }

        public string Gender { get; set; }

        public string Level { get; set; }

        public string ProofDocument { get; set; }

        public string Status { get; set; }

        public string AdminFeedback { get; set; }

        public DateTime ApplicationDate { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }
    }
}