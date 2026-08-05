using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DUTResManagementSystem.Models;

namespace DUTResManagementSystem.Controllers
{
    public class TechnicianController : Controller
    {
        private readonly ResContext db = new ResContext();

        // ─────────────────────────────────────────────────────────────
        // REGISTRATION
        // ─────────────────────────────────────────────────────────────

        // GET: Technician/Register
        public ActionResult Register()
        {
            return View();
        }

        // POST: Technician/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Technician technician)
        {
            if (!ModelState.IsValid)
                return View(technician);

            bool emailExists = db.Technicians.Any(t => t.Email == technician.Email);

            if (emailExists)
            {
                ViewBag.Error = "A technician with this email address already exists.";
                return View(technician);
            }

            technician.AvailabilityStatus = true;
            technician.DateAdded = DateTime.Now;

            db.Technicians.Add(technician);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Technician registered successfully.";
            return RedirectToAction("Register");
        }

        // ─────────────────────────────────────────────────────────────
        // AUTHENTICATION
        // ─────────────────────────────────────────────────────────────

        // GET: Technician/TechLogin
        public ActionResult TechLogin()
        {
            return View();
        }

        // POST: Technician/TechLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TechLogin(string email, string password)
        {
            var technician = db.Technicians
                .FirstOrDefault(t => t.Email == email && t.Password == password);

            if (technician == null)
            {
                ViewBag.ErrorMessage = "Invalid email or password. Please try again.";
                return View();
            }

            Session["TechnicianID"] = technician.TechnicianID;
            Session["TechnicianName"] = technician.FullName;
            Session["TechnicianRole"] = "Technician";

            return RedirectToAction("TechDashboard");
        }

        // GET: Technician/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("TechLogin");
        }

        // ─────────────────────────────────────────────────────────────
        // DASHBOARD
        // ─────────────────────────────────────────────────────────────

        // GET: Technician/TechDashboard
        public ActionResult TechDashboard()
        {
            if (Session["TechnicianID"] == null)
                return RedirectToAction("TechLogin", "Technician");

            var technicianId = (int)Session["TechnicianID"];
            var jobs = db.Maintenances
                .Where(m => m.TechnicianID == technicianId)
                .OrderByDescending(m => m.DateReported)
                .ToList();

            return View(jobs);
        }

        // ─────────────────────────────────────────────────────────────
        // JOB COMPLETION
        // ─────────────────────────────────────────────────────────────

        // POST: Technician/CompleteJob
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteJob(int maintenanceId, HttpPostedFileBase proofImage)
        {
            if (Session["TechnicianID"] == null)
                return RedirectToAction("TechLogin");

            var job = db.Maintenances.Find(maintenanceId);

            if (job == null)
            {
                TempData["Error"] = "Maintenance job not found.";
                return RedirectToAction("TechDashboard");
            }

            // Save proof-of-completion image if provided
            if (proofImage != null && proofImage.ContentLength > 0)
            {
                string uploadFolder = Server.MapPath("~/Uploads/CompletedJobs/");

                if (!System.IO.Directory.Exists(uploadFolder))
                    System.IO.Directory.CreateDirectory(uploadFolder);

                string fileName = System.IO.Path.GetFileName(proofImage.FileName);
                string fullPath = System.IO.Path.Combine(uploadFolder, fileName);

                proofImage.SaveAs(fullPath);

                job.CompletionImage = "/Uploads/CompletedJobs/" + fileName;
            }

            job.Status = "Resolved";
            job.DateResolved = DateTime.Now;

            db.SaveChanges();

            TempData["Success"] = "Job marked as resolved successfully.";
            return RedirectToAction("TechDashboard");
        }
    }
}
