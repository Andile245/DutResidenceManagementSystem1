using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DUTResManagementSystem.ViewModels
{
public class AnnouncementViewModel
{
    public int AnnouncementID { get; set; }

    [Required]
    [Display(Name = "Title")]
    public string Title { get; set; }

    [Required]
    [Display(Name = "Content")]
    public string Content { get; set; }

    [Display(Name = "Priority")]
    public string Priority { get; set; }

    [Display(Name = "Expiry Date")]
    public DateTime? ExpiryDate { get; set; }

    [Display(Name = "Target Audience")]
    public string TargetAudience { get; set; }
}
}