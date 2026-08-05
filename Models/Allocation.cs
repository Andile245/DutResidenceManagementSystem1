using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace DUTResManagementSystem.Models
{
    public class Allocation
    {
        [Key]
        public int AllocationID { get; set; }

        public int? StudentID { get; set; }

        public int? ResidenceID { get; set; }

        public string RoomNumber { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }

        [ForeignKey("ResidenceID")]
        public virtual Residence Residence { get; set; }
    }
}