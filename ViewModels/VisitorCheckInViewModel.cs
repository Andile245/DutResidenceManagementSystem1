using DUTResManagementSystem.Models;
using System.Collections.Generic;
using System.Web.Mvc;

namespace DUTResManagementSystem.ViewModels
{
    public class VisitorCheckInViewModel
    {
        public Visitor Visitor { get; set; } = new Visitor();
        public List<SelectListItem> Students { get; set; } = new List<SelectListItem>();
        public string ScannedDocumentData { get; set; }
    }
}
