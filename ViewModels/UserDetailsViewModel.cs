using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DUTResManagementSystem.Models;

namespace DUTResManagementSystem.ViewModels
{
    public class UserDetailsViewModel
    {
        public List<Student> Students { get; set; }
        public List<Staff> Staff { get; set; }
    }
}