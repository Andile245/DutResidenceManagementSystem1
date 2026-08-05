using DUTResManagementSystem.Models;
using DUTResManagementSystem.ViewModels;
using DUTResSystemWebApp.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Stripe;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;

namespace DUTResSystemWebApp.Controllers
{
    public class StudentController : Controller
    {
        private ResContext db = new ResContext();
        private readonly NotificationService _notificationService;
        public StudentController()
        {
            _notificationService = new NotificationService();
        }

        // GET: Student/Notifications
        public ActionResult Notifications()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            var studentId = (int)Session["StudentID"];
            return View(_notificationService.GetAllNotifications(studentId, "Student", 50));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkNotificationRead(int id)
        {
            if (Session["StudentID"] == null) return new HttpStatusCodeResult(403);
            var studentId = (int)Session["StudentID"];
            var notification = db.Notifications.FirstOrDefault(n => n.NotificationID == id && n.UserID == studentId && n.UserType == "Student");
            if (notification != null)
            {
                notification.IsRead = true;
                db.SaveChanges();
            }
            return RedirectToAction("Notifications");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult MarkAllNotificationsRead()
        {
            if (Session["StudentID"] == null) return new HttpStatusCodeResult(403);
            _notificationService.MarkAllNotificationsAsRead((int)Session["StudentID"], "Student");
            return RedirectToAction("Notifications");
        }

        // GET: Student/Dashboard
        public ActionResult Dashboard()
        {
            if (Session["StudentID"] == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = (int)Session["StudentID"];
            var student = db.Students
                .Include(s => s.Residence)
                .Include(s => s.Room)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            // If student is not allocated to a residence, redirect to waiting page
            if (student.ResidenceID == null)
            {
                return RedirectToAction("NotAllocated", "Student");
            }

            int unreadCount = _notificationService.GetUnreadAnnouncementCount(studentId);
            var unreadAnnouncements = _notificationService.GetUnreadAnnouncements(studentId);

            var viewModel = new StudentDashboardViewModel
            {
                Student = student,
                Residence = student.Residence,
                MaintenanceRequests = db.Maintenances
                    .Where(m => m.StudentID == studentId)
                    .OrderByDescending(m => m.DateReported)
                    .Take(5)
                    .ToList(),
                Announcements = db.Announcements
    .Include(a => a.Staff)
    .Where(a =>
        (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now) &&
        (
            a.TargetAudience == "Everyone" ||
            a.TargetAudience == "Students" ||
            (a.ResidenceID != null && a.ResidenceID == student.ResidenceID)
        )
    )
    .OrderByDescending(a => a.DatePosted)
    .Take(5)
    .ToList(),
                UnreadAnnouncementCount = unreadCount,
                UnreadAnnouncements = unreadAnnouncements.Take(3).ToList()
            };

            Session["UnreadAnnouncementCount"] = unreadCount;

            var app = db.ResidenceApplications
                        .FirstOrDefault(x => x.StudentID == studentId);

            ViewBag.Status = app != null ? app.Status : "No Application";
            ViewBag.Feedback = app != null ? app.AdminFeedback : "";

            return View(viewModel);
        }

        // GET: Student/NotAllocated
        // Shown to students who are logged in but not yet allocated to a residence
        public ActionResult NotAllocated()
        {
            if (Session["StudentID"] == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = (int)Session["StudentID"];
            var student = db.Students.Find(studentId);

            if (student == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            // If they somehow got allocated since logging in, send them to the dashboard
            if (student.ResidenceID != null)
            {
                return RedirectToAction("Dashboard", "Student");
            }

            // Pass the student's name to the view for the personalised message
            ViewBag.StudentName = student.FirstName;

            var app = db.ResidenceApplications
                        .FirstOrDefault(x => x.StudentID == studentId);

            ViewBag.ApplicationStatus = app != null ? app.Status : "No Application";

            return View();
        }

        private bool IsStudentAllocated()
        {
            if (Session["StudentID"] == null) return false;
            int studentId = (int)Session["StudentID"];
            var student = db.Students.Find(studentId);
            return student != null && student.ResidenceID != null;
        }


        // GET: Student/Profile

        public ActionResult Profile()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            // IMPORTANT: Include Residence data
            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            return View(student);
        }

        // ---------------------------------------------------------------
        // Replace these methods in your StudentController
        // ---------------------------------------------------------------

        // GET: Student/Maintenance
        public ActionResult Maintenance()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var student = db.Students.Find(studentId);

            // Tell the view whether the student has a room so it can show/hide the report button
            ViewBag.HasRoom = student?.RoomID != null;

            var maintenanceRequests = db.Maintenances
                .Where(m => m.StudentID == studentId)
                .OrderByDescending(m => m.DateReported)
                .ToList();

            return View(maintenanceRequests);
        }


        // GET: Student/ReportMaintenance
        public ActionResult ReportMaintenance()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            // Load student with their room so we can pre-fill the room number
            var student = db.Students
                .Include(s => s.Room)
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            // Block the student if they have no room assigned yet
            if (student?.RoomID == null)
            {
                TempData["ErrorMessage"] = "You must be allocated to a room before you can submit a maintenance request. Please contact your building manager.";
                return RedirectToAction("Maintenance");
            }

            var model = new MaintenanceReportViewModel
            {
                RoomNumber = student.Room.RoomNumber
            };

            ViewBag.RoomNumber = student.Room.RoomNumber;
            ViewBag.RoomType = student.Room.RoomType;
            ViewBag.ResidenceName = student.Residence?.Name;
            ViewBag.HasRoom = true;

            return View(model);
        }


        // POST: Student/ReportMaintenance
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportMaintenance(MaintenanceReportViewModel model)
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var student = db.Students
                .Include(s => s.Room)
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            // Block if no room assigned — prevents bypassing via direct POST
            if (student?.RoomID == null)
            {
                TempData["ErrorMessage"] = "You must be allocated to a room before you can submit a maintenance request.";
                return RedirectToAction("Maintenance");
            }

            if (ModelState.IsValid)
            {
                // Handle image upload
                string filePath = null;
                if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
                {
                    string uploadsFolder = Server.MapPath("~/Uploads");
                    if (!System.IO.Directory.Exists(uploadsFolder))
                        System.IO.Directory.CreateDirectory(uploadsFolder);

                    string fileName = Guid.NewGuid() + System.IO.Path.GetExtension(model.ImageFile.FileName);
                    string fullPath = System.IO.Path.Combine(uploadsFolder, fileName);
                    model.ImageFile.SaveAs(fullPath);
                    filePath = "/Uploads/" + fileName;
                }

                DateTime reportedAt = DateTime.Now;
                bool safetyCritical = model.IssueType == MaintenanceIssueType.Security;
                string priority = safetyCritical
                    ? "Critical"
                    : (model.IssueType == MaintenanceIssueType.Electrical || model.IssueType == MaintenanceIssueType.Plumbing ? "High" : "Normal");

                var maintenance = new Maintenance
                {
                    StudentID = studentId,
                    RoomNumber = student?.Room?.RoomNumber ?? model.RoomNumber,
                    RoomID = student?.RoomID,
                    IssueType = model.IssueType,
                    IssueDescription = model.IssueDescription,
                    DateReported = reportedAt,
                    Status = "Pending",
                    Priority = priority,
                    IsSafetyCritical = safetyCritical,
                    TargetResponseBy = safetyCritical
                        ? reportedAt.AddHours(4)
                        : (model.IssueType == MaintenanceIssueType.Electrical || model.IssueType == MaintenanceIssueType.Plumbing ? reportedAt.AddHours(24) : reportedAt.AddDays(3)),
                    ImagePath = filePath
                };

                db.Maintenances.Add(maintenance);
                db.SaveChanges();

                // Notify the building manager of this residence
                var notificationService = new NotificationService();
                notificationService.NotifyBuildingManagerMaintenance(maintenance.MaintenanceID);

                TempData["SuccessMessage"] = "Your maintenance request has been submitted successfully. The building manager has been notified.";
                return RedirectToAction("Maintenance");
            }

            // Re-populate ViewBag on validation failure
            ViewBag.RoomNumber = student?.Room?.RoomNumber;
            ViewBag.RoomType = student?.Room?.RoomType;
            ViewBag.ResidenceName = student?.Residence?.Name;
            ViewBag.HasRoom = student?.Room != null;

            return View(model);
        }


        // GET: Student/Announcements
        public ActionResult Announcements()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];
            var student = db.Students.Find(studentId);

            if (student == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            // Get announcements for students (Everyone + Students announcements)
            var announcements = db.Announcements
                .Where(a => (a.TargetAudience == "Everyone" || a.TargetAudience == "Students") &&
                           (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now))
                .OrderByDescending(a => a.DatePosted)
                .ToList();

            return View(announcements);
        }
        //GET Apply Residence
        public ActionResult ApplyResidence()
        {
            if (Session["StudentID"] == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            ViewBag.Residences = db.Residences.ToList();

            return View();
        }
        //POST Apply Residence
       /* [HttpPost]
        public ActionResult ApplyResidence(HttpPostedFileBase proof, int residenceId)
        {
            if (Session["StudentID"] == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = Convert.ToInt32(Session["StudentID"]);

            string filePath = "";

            if (proof != null && proof.ContentLength > 0)
            {
                filePath = "/Uploads/" + proof.FileName;
                proof.SaveAs(Server.MapPath(filePath));
            }

            ResContext db = new ResContext();

            db.StudentID = studentId;
            app.ResidenceID = residenceId;
            app.ProofDocument = filePath;
            app.Status = "Pending";
            app.ApplicationDate = DateTime.Now;

            db.DUTResidenceManagementDB.Add(app);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Application submitted successfully.";

            return RedirectToAction("Dashboard");
        }*/

        // GET: Student/Logout
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // GET: Student/DownloadProofOfRegistration
        public ActionResult DownloadProofOfRegistration()
        {
            if (Session["StudentID"] == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = (int)Session["StudentID"];
            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 25, 25);
                    PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                    document.Open();

                    // Add title
                    Paragraph title = new Paragraph("PROOF OF REGISTRATION",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, Font.BOLD));
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    document.Add(title);

                    // Add DUT header
                    Paragraph header = new Paragraph("DURBAN UNIVERSITY OF TECHNOLOGY",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, Font.BOLD));
                    header.Alignment = Element.ALIGN_CENTER;
                    header.SpacingAfter = 10;
                    document.Add(header);

                    Paragraph subHeader = new Paragraph("RESIDENCE MANAGEMENT SYSTEM",
                        FontFactory.GetFont(FontFactory.HELVETICA, 12));
                    subHeader.Alignment = Element.ALIGN_CENTER;
                    subHeader.SpacingAfter = 30;
                    document.Add(subHeader);

                    // Add student information
                    document.Add(new Paragraph("STUDENT INFORMATION:",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, Font.BOLD)));

                    AddInfoLine(document, "Student Number:", student.StudentNumber);
                    AddInfoLine(document, "Full Name:", $"{student.FirstName} {student.LastName}");
                    AddInfoLine(document, "Email:", student.Email);
                    AddInfoLine(document, "Faculty:", student.Faculty ?? "Not specified");
                    AddInfoLine(document, "Year of Study:", student.YearOfStudy.ToString() ?? "Not specified");
                    AddInfoLine(document, "Date Registered:", student.DateRegistered.ToString("dd MMMM yyyy") ?? "Not specified");
                    document.Add(Chunk.NEWLINE);

                    // Add residence information
                    document.Add(new Paragraph("RESIDENCE INFORMATION:",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, Font.BOLD)));

                    if (student.Residence != null)
                    {
                        AddInfoLine(document, "Residence Name:", student.Residence.Name);
                        AddInfoLine(document, "Address:", student.Residence.Address ?? "Not specified");
                        AddInfoLine(document, "Contact Number:", student.Residence.ContactNumber ?? "Not specified");
                    }
                    else
                    {
                        document.Add(new Paragraph("Not allocated to any residence",
                            FontFactory.GetFont(FontFactory.HELVETICA, 10, Font.ITALIC)));
                    }

                    document.Add(Chunk.NEWLINE);
                    document.Add(Chunk.NEWLINE);

                    // Add footer
                    Paragraph footer = new Paragraph($"Generated on: {DateTime.Now:dd MMMM yyyy HH:mm}",
                        FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8));
                    footer.Alignment = Element.ALIGN_RIGHT;
                    document.Add(footer);

                    document.Close();

                    // Return PDF file
                    byte[] bytes = memoryStream.ToArray();
                    return File(bytes, "application/pdf",
                        $"ProofOfRegistration_{student.StudentNumber}_{DateTime.Now:yyyyMMdd}.pdf");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error generating PDF: " + ex.Message;
                return RedirectToAction("Profile");
            }
        }

        private void AddInfoLine(Document document, string label, string value)
        {
            Paragraph paragraph = new Paragraph();
            paragraph.Add(new Chunk(label, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10)));
            paragraph.Add(new Chunk($" {value}", FontFactory.GetFont(FontFactory.HELVETICA, 10)));
            paragraph.SpacingAfter = 5;
            document.Add(paragraph);
        }

        // GET: Student/EmailProofOfRegistration
        public ActionResult EmailProofOfRegistration()
        {
            if (Session["StudentID"] == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = (int)Session["StudentID"];
            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student == null)
            {
                return RedirectToAction("StudentLogin", "Auth");
            }

            try
            {
                // Generate PDF
                byte[] pdfBytes;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    Document document = new Document(PageSize.A4, 50, 50, 25, 25);
                    PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                    document.Open();

                    // Add content (same as DownloadProofOfRegistration)
                    Paragraph title = new Paragraph("PROOF OF REGISTRATION",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, Font.BOLD));
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20;
                    document.Add(title);

                    // Add student info
                    AddInfoLine(document, "Student Number:", student.StudentNumber);
                    AddInfoLine(document, "Full Name:", $"{student.FirstName} {student.LastName}");
                    AddInfoLine(document, "Email:", student.Email);
                    AddInfoLine(document, "Faculty:", student.Faculty ?? "Not specified");
                    AddInfoLine(document, "Year of Study:", student.YearOfStudy.ToString() ?? "Not specified");

                    document.Close();
                    pdfBytes = memoryStream.ToArray();
                }

                // Send email
                SendEmailWithAttachment(student.Email, pdfBytes, student);

                TempData["SuccessMessage"] = "Proof of Registration has been sent to your email successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error sending email: " + ex.Message;
            }

            return RedirectToAction("Profile");
        }

        private void SendEmailWithAttachment(string toEmail, byte[] attachmentBytes, Student student)
        {
            try
            {
                string smtpHost = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.office365.com";
                int smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                string smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"] ?? "22355596@dut4life.ac.za";
                string smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "Kaylan@2004";
                string fromEmail = ConfigurationManager.AppSettings["SmtpFromEmail"] ?? "22355596@dut4life.ac.za";
                string fromName = ConfigurationManager.AppSettings["SmtpFromName"] ?? "DUT Residence System";

                using (SmtpClient smtpClient = new SmtpClient(smtpHost, smtpPort))
                {
                    smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    smtpClient.EnableSsl = true;

                    MailMessage mailMessage = new MailMessage();
                    mailMessage.From = new MailAddress(fromEmail, fromName);
                    mailMessage.To.Add(toEmail);
                    mailMessage.Subject = "Proof of Registration - DUT Residence";
                    mailMessage.Body = $@"
Dear {student.FirstName} {student.LastName},

Please find attached your Proof of Registration for DUT Residence.

Student Number: {student.StudentNumber}
Date Generated: {DateTime.Now:dd MMMM yyyy HH:mm}

If you have any questions, please contact the residence administration.

Best regards,
DUT Residence Management System
";

                    // Attach PDF
                    using (MemoryStream memoryStream = new MemoryStream(attachmentBytes))
                    {
                        Attachment attachment = new Attachment(memoryStream,
                            $"ProofOfRegistration_{student.StudentNumber}_{DateTime.Now:yyyyMMdd}.pdf",
                            "application/pdf");
                        mailMessage.Attachments.Add(attachment);

                        smtpClient.Send(mailMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to send email: " + ex.Message);
            }
        }
        // ---------------------------------------------------------------
        // Paste these methods into your StudentController
        // ---------------------------------------------------------------

        // GET: Student/RoomChangeRequest
        public ActionResult RoomChangeRequest()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var student = db.Students
                .Include(s => s.Room)
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            // Must have a room before requesting a change
            if (student?.RoomID == null)
            {
                TempData["ErrorMessage"] = "You must be allocated to a room before you can request a room change.";
                return RedirectToAction("Dashboard");
            }

            // Check if student already has a pending request
            bool hasPending = db.RoomChangeRequests
                .Any(r => r.StudentID == studentId && r.Status == "Pending");

            if (hasPending)
            {
                TempData["ErrorMessage"] = "You already have a pending room change request. Please wait for it to be reviewed before submitting another.";
                return RedirectToAction("MyRoomChangeRequests");
            }

            ViewBag.CurrentRoom = student.Room;
            ViewBag.ResidenceName = student.Residence?.Name;

            return View(new RoomChangeRequestViewModel());
        }


        // POST: Student/RoomChangeRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RoomChangeRequest(RoomChangeRequestViewModel model)
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var student = db.Students
                .Include(s => s.Room)
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student?.RoomID == null)
            {
                TempData["ErrorMessage"] = "You must be allocated to a room before requesting a change.";
                return RedirectToAction("Dashboard");
            }

            // Double-check no pending request exists
            bool hasPending = db.RoomChangeRequests
                .Any(r => r.StudentID == studentId && r.Status == "Pending");

            if (hasPending)
            {
                TempData["ErrorMessage"] = "You already have a pending room change request.";
                return RedirectToAction("MyRoomChangeRequests");
            }

            if (ModelState.IsValid)
            {
                // Handle optional document upload
                string documentPath = null;
                if (model.DocumentFile != null && model.DocumentFile.ContentLength > 0)
                {
                    // Only allow PDF, Word, and image files
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                    string ext = System.IO.Path.GetExtension(model.DocumentFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                    {
                        ModelState.AddModelError("DocumentFile",
                            "Only PDF, Word documents, and images are allowed.");
                        ViewBag.CurrentRoom = student.Room;
                        ViewBag.ResidenceName = student.Residence?.Name;
                        return View(model);
                    }

                    string uploadsFolder = Server.MapPath("~/Uploads/RoomChangeDocuments");
                    if (!System.IO.Directory.Exists(uploadsFolder))
                        System.IO.Directory.CreateDirectory(uploadsFolder);

                    string fileName = Guid.NewGuid() + ext;
                    string fullPath = System.IO.Path.Combine(uploadsFolder, fileName);
                    model.DocumentFile.SaveAs(fullPath);
                    documentPath = "/Uploads/RoomChangeDocuments/" + fileName;
                }

                var request = new RoomChangeRequest
                {
                    StudentID = studentId,
                    CurrentRoomID = student.RoomID.Value,
                    RequestedRoomID = null, // building manager decides
                    Reason = model.Reason,
                    DocumentPath = documentPath,
                    Status = "Pending",
                    DateRequested = DateTime.Now
                };

                db.RoomChangeRequests.Add(request);
                db.SaveChanges();

                // Notify building manager
                var notificationService = new NotificationService();
                notificationService.CreateNotification(
                    db.Staffs.FirstOrDefault(s => s.ResidenceID == student.ResidenceID &&
                                                  s.Role == "Building Manager")?.StaffID ?? 0,
                    "BuildingManager",
                    "New Room Change Request",
                    $"Student {student.FirstName} {student.LastName} has requested a room change from Room {student.Room.RoomNumber}.",
                    "RoomChangeRequest",
                    request.RequestID,
                    "RoomChangeRequest"
                );

                TempData["SuccessMessage"] = "Your room change request has been submitted successfully. The building manager will review it shortly.";
                return RedirectToAction("MyRoomChangeRequests");
            }

            // Re-populate ViewBag on validation failure
            ViewBag.CurrentRoom = student.Room;
            ViewBag.ResidenceName = student.Residence?.Name;
            return View(model);
        }


        // GET: Student/MyRoomChangeRequests
        public ActionResult MyRoomChangeRequests()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var requests = db.RoomChangeRequests
                .Where(r => r.StudentID == studentId)
                .OrderByDescending(r => r.DateRequested)
                .ToList();

            // Manually load CurrentRoom for each request to avoid EF include issues
            foreach (var r in requests)
            {
                r.CurrentRoom = db.Rooms.Find(r.CurrentRoomID);
                r.RequestedRoom = r.RequestedRoomID.HasValue
                                  ? db.Rooms.Find(r.RequestedRoomID.Value)
                                  : null;
                if (r.ReviewedByStaffID.HasValue)
                    r.ReviewedBy = db.Staffs.Find(r.ReviewedByStaffID.Value);
            }

            return View(requests);
        }
       
        

        // ── HELPER (also add to StaffController if not already there) ─────────────────
      
        // Add this helper method to your StudentController
        private DateTime GetSouthAfricaTime()
        {
            TimeZoneInfo saTimeZone;
            try
            {
                // Try Windows time zone
                saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            }
            catch
            {
                try
                {
                    // Try Linux/Mac time zone
                    saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
                }
                catch
                {
                    // Fallback to UTC+2
                    saTimeZone = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "South Africa Standard Time", "South Africa Standard Time");
                }
            }

            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, saTimeZone);
        }

        public ActionResult MyCheckInStatus()
        {
            if (Session["StudentID"] == null)
                return RedirectToAction("StudentLogin", "Auth");

            int studentId = (int)Session["StudentID"];

            var student = db.Students
                .Include(s => s.Room)
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            // Find their check-in record if it exists
            var checkIn = db.ResidenceCheckIns
                .Include(c => c.Residence)
                .FirstOrDefault(c => c.StudentID == studentId);

            ViewBag.Student = student;
            ViewBag.CheckIn = checkIn;
            ViewBag.Residence = student?.Residence;

            return View();
        }

        // GET: Student/ScanCheckIn?token=abc123
        // Called when student opens the QR code URL on their phone
        public ActionResult ScanCheckIn(string token)
        {
            // If student is not logged in, save the token and send them to login
            if (Session["StudentID"] == null)
            {
                Session["PendingCheckInToken"] = token;
                TempData["InfoMessage"] =
                    "Please log in with your student account to complete your check-in.";
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = (int)Session["StudentID"];

            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Result = "error";
                ViewBag.Message = "Invalid QR code. Please ask your building manager for a new one.";
                return View("CheckInResult");
            }

            // Find the record matching this exact token AND this exact student
            // If the token belongs to a different student it will return null
            var record = db.ResidenceCheckIns
                .Include(c => c.Residence)
                .FirstOrDefault(c => c.QRToken == token &&
                                     c.StudentID == studentId);

            if (record == null)
            {
                // Could be wrong student or invalid token
                ViewBag.Result = "error";
                ViewBag.Message =
                    "This QR code is not linked to your account. " +
                    "Each student has their own personal QR code. " +
                    "Please use the one generated specifically for you by your building manager.";
                return View("CheckInResult");
            }

            if (record.HasCheckedIn)
            {
                // Format the check-in time in SA time
                string checkInTimeFormatted = record.CheckInTime.HasValue
                    ? record.CheckInTime.Value.ToString("dd MMM yyyy 'at' HH:mm")
                    : "unknown time";

                ViewBag.Result = "already";
                ViewBag.Message =
                    $"You have already checked in to {record.Residence?.Name} " +
                    $"on {checkInTimeFormatted}.";
                ViewBag.ResidenceName = record.Residence?.Name;
                return View("CheckInResult");
            }

            // All good — record the check-in using SA time
            DateTime saTime = GetSouthAfricaTime();
            record.HasCheckedIn = true;
            record.CheckInTime = saTime;
            db.SaveChanges();

            // Clear any pending token from session
            Session.Remove("PendingCheckInToken");

            ViewBag.Result = "success";
            ViewBag.Message =
                $"Check-in successful! Welcome to {record.Residence?.Name}. " +
                "Your building manager can now allocate you to a room.";
            ViewBag.ResidenceName = record.Residence?.Name;
            ViewBag.CheckInTime = record.CheckInTime.Value.ToString("dd MMM yyyy 'at' HH:mm");

            return View("CheckInResult");
        }

        // GET: Student/ScanCheckOut?token=abc123
        // Called when student opens the check-out QR code URL on their phone
        public ActionResult ScanCheckOut(string token)
        {
            // If student is not logged in, save the token and send them to login
            if (Session["StudentID"] == null)
            {
                Session["PendingCheckOutToken"] = token;
                TempData["InfoMessage"] =
                    "Please log in with your student account to complete your check-out.";
                return RedirectToAction("StudentLogin", "Auth");
            }

            int studentId = (int)Session["StudentID"];

            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Result = "error";
                ViewBag.Message = "Invalid QR code. Please ask your building manager for a new one.";
                return View("CheckOutResult");
            }

            // Find the record matching this exact token AND this exact student
            var record = db.ResidenceCheckIns
                .Include(c => c.Residence)
                .FirstOrDefault(c => c.QRToken == token &&
                                     c.StudentID == studentId);

            if (record == null)
            {
                ViewBag.Result = "error";
                ViewBag.Message =
                    "This QR code is not linked to your account. " +
                    "Each student has their own personal QR code. " +
                    "Please use the one generated specifically for you by your building manager.";
                return View("CheckOutResult");
            }

            // Check if student has already checked out
            if (record.HasCheckedOut)
            {
                string checkOutTimeFormatted = record.CheckOutTime.HasValue
                    ? record.CheckOutTime.Value.ToString("dd MMM yyyy 'at' HH:mm")
                    : "unknown time";

                ViewBag.Result = "already";
                ViewBag.Message =
                    $"You have already checked out from {record.Residence?.Name} " +
                    $"on {checkOutTimeFormatted}.";
                ViewBag.ResidenceName = record.Residence?.Name;
                return View("CheckOutResult");
            }

            // Student must have checked in first before they can check out
            if (!record.HasCheckedIn)
            {
                ViewBag.Result = "error";
                ViewBag.Message =
                    "You cannot check out because you haven't checked in yet. " +
                    "Please scan your check-in QR code first.";
                return View("CheckOutResult");
            }

            // =========================================================
            // CHECK-OUT PROCESS - Deallocate student from room
            // =========================================================

            // Get the student with their room information
            var student = db.Students
                .Include(s => s.Room)
                .FirstOrDefault(s => s.StudentID == studentId);

            string roomNumber = null;
            bool hadRoom = false;

            if (student != null && student.RoomID != null)
            {
                hadRoom = true;
                roomNumber = student.Room?.RoomNumber ?? "Unknown";

                // Get the room to update its status
                var room = db.Rooms.Find(student.RoomID);
                if (room != null)
                {
                    room.Status = "Available";
                }

                // Remove student from room
                student.RoomID = null;

                // Send notification about room deallocation
                try
                {
                    var notificationService = new NotificationService();
                    notificationService.NotifyStudentRoomDeallocation(studentId, roomNumber);

                    // Also notify building manager
                    var buildingManager = db.Staffs
                        .FirstOrDefault(s => s.ResidenceID == record.ResidenceID &&
                                            s.Role == "Building Manager");
                    if (buildingManager != null)
                    {
                        notificationService.CreateNotification(
                            buildingManager.StaffID,
                            "BuildingManager",
                            "Student Checked Out",
                            $"{student.FirstName} {student.LastName} has checked out and been removed from Room {roomNumber}.",
                            "CheckOut",
                            record.CheckInID,
                            "ResidenceCheckIn"
                        );
                    }
                }
                catch { }
            }

            // Record the check-out using SA time
            DateTime saTime = GetSouthAfricaTime();
            record.HasCheckedOut = true;
            record.CheckOutTime = saTime;
            db.SaveChanges();

            // Clear any pending token from session
            Session.Remove("PendingCheckOutToken");

            // Prepare success message
            string successMessage = hadRoom
                ? $"Check-out successful! You have been removed from Room {roomNumber}. Thank you for staying with us."
                : $"Check-out successful! Thank you for staying with us.";

            ViewBag.Result = "success";
            ViewBag.Message = successMessage;
            ViewBag.ResidenceName = record.Residence?.Name;
            ViewBag.CheckOutTime = record.CheckOutTime.Value.ToString("dd MMM yyyy 'at' HH:mm");
            ViewBag.RoomNumber = roomNumber;
            ViewBag.HadRoom = hadRoom;

            return View("CheckOutResult");
        }
        public ActionResult ResidenceChangeRequest()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var student = db.Students
                .Include(s => s.Residence)
                .Include(s => s.Room)
                .FirstOrDefault(s => s.StudentID == studentId);

            // Student must be allocated to a residence to request a change
            if (student?.ResidenceID == null)
            {
                TempData["ErrorMessage"] =
                    "You must be allocated to a residence before you can request a residence change.";
                return RedirectToAction("Dashboard");
            }

            // Block if a pending request already exists
            bool hasPending = db.ResidenceChangeRequests
                .Any(r => r.StudentID == studentId && r.Status == "Pending");

            if (hasPending)
            {
                TempData["ErrorMessage"] =
                    "You already have a pending residence change request. " +
                    "Please wait for the admin to review it before submitting another.";
                return RedirectToAction("MyResidenceChangeRequests");
            }

            ViewBag.CurrentResidence = student.Residence;
            ViewBag.CurrentRoom = student.Room;

            return View(new ResidenceChangeRequestViewModel());
        }

        // ── POST: Student/ResidenceChangeRequest ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResidenceChangeRequest(ResidenceChangeRequestViewModel model)
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student?.ResidenceID == null)
            {
                TempData["ErrorMessage"] =
                    "You must be allocated to a residence before requesting a change.";
                return RedirectToAction("Dashboard");
            }

            // Double-check no pending request
            bool hasPending = db.ResidenceChangeRequests
                .Any(r => r.StudentID == studentId && r.Status == "Pending");

            if (hasPending)
            {
                TempData["ErrorMessage"] = "You already have a pending residence change request.";
                return RedirectToAction("MyResidenceChangeRequests");
            }

            if (ModelState.IsValid)
            {
                // Handle optional document upload
                string documentPath = null;
                if (model.DocumentFile != null && model.DocumentFile.ContentLength > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                    string ext = System.IO.Path.GetExtension(model.DocumentFile.FileName).ToLower();

                    if (!System.Array.Exists(allowedExtensions, e => e == ext))
                    {
                        TempData["ErrorMessage"] = "Invalid file type. Allowed: PDF, Word, JPG, PNG.";
                        ViewBag.CurrentResidence = student.Residence;
                        return View(model);
                    }

                    if (model.DocumentFile.ContentLength > 5 * 1024 * 1024) // 5 MB limit
                    {
                        TempData["ErrorMessage"] = "File size must be less than 5 MB.";
                        ViewBag.CurrentResidence = student.Residence;
                        return View(model);
                    }

                    string uploadsFolder = Server.MapPath("~/Uploads/ResidenceChangeRequests/");
                    if (!System.IO.Directory.Exists(uploadsFolder))
                        System.IO.Directory.CreateDirectory(uploadsFolder);

                    string fileName = $"ResChange_{studentId}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    model.DocumentFile.SaveAs(System.IO.Path.Combine(uploadsFolder, fileName));
                    documentPath = $"/Uploads/ResidenceChangeRequests/{fileName}";
                }

                var request = new ResidenceChangeRequest
                {
                    StudentID = studentId,
                    CurrentResidenceID = student.ResidenceID.Value,
                    Reason = model.Reason,
                    DocumentPath = documentPath,
                    Status = "Pending",
                    DateRequested = DateTime.Now
                };

                db.ResidenceChangeRequests.Add(request);

                // Notify admin via in-app notification
                var adminIds = db.Staffs
                    .Where(s => s.Role == "Admin" && s.IsActive)
                    .Select(s => s.StaffID)
                    .ToList();

                foreach (var adminId in adminIds)
                {
                    // We store admin notifications differently — use a simple approach
                    // In-app: create a notification for each admin
                }

                db.SaveChanges();

                // Email all admins
                try
                {
                    var ns = new NotificationService();
                    var admins = db.Staffs.Where(s => s.Role == "Admin" && s.IsActive).ToList();
                    foreach (var admin in admins)
                    {
                        ns.SendEmail(
                            admin.Email,
                            "New Residence Change Request — DUT Residences",
                            $"Dear {admin.FirstName},\n\n" +
                            $"A new residence change request has been submitted.\n\n" +
                            $"Student  : {student.FirstName} {student.LastName} ({student.StudentNumber})\n" +
                            $"From     : {student.Residence?.Name}\n" +
                            $"Reason   : {model.Reason.Substring(0, Math.Min(200, model.Reason.Length))}...\n\n" +
                            "Please log in to the admin portal to review this request.\n\n" +
                            "DUT Residence Management"
                        );
                    }
                }
                catch { }

                TempData["SuccessMessage"] =
                    "Your residence change request has been submitted successfully. " +
                    "The admin will review your request and notify you of their decision.";

                return RedirectToAction("MyResidenceChangeRequests");
            }

            ViewBag.CurrentResidence = student.Residence;
            return View(model);
        }

        // ── GET: Student/MyResidenceChangeRequests ────────────────────────────────────
        public ActionResult MyResidenceChangeRequests()
        {
            if (Session["StudentID"] == null) return RedirectToAction("StudentLogin", "Auth");
            if (!IsStudentAllocated()) return RedirectToAction("NotAllocated");

            int studentId = (int)Session["StudentID"];

            var requests = db.ResidenceChangeRequests
                .Include(r => r.CurrentResidence)
                .Include(r => r.ReviewedBy)
                .Where(r => r.StudentID == studentId)
                .OrderByDescending(r => r.DateRequested)
                .ToList();

            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            ViewBag.Student = student;
            ViewBag.CurrentResidence = student?.Residence;

            return View(requests);
        }

        // POST: Student/ConfirmResolution////
        //nkululeko
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmResolution(int maintenanceId)
        {
            if (Session["StudentID"] == null)
                return RedirectToAction("StudentLogin", "Auth");

            int studentId = (int)Session["StudentID"];

            var maintenance = db.Maintenances
                .Include(m => m.Student)
                .FirstOrDefault(m => m.MaintenanceID == maintenanceId
                                  && m.StudentID == studentId);

            if (maintenance == null)
            {
                TempData["ErrorMessage"] = "Maintenance not found.";
                return RedirectToAction("Maintenance");
            }

            if (maintenance.Status != "Resolved")
            {
                TempData["ErrorMessage"] = "Task not yet resolved.";
                return RedirectToAction("Maintenance");
            }

            var student = maintenance.Student;

            // ✅ Update maintenance
            maintenance.Status = "Confirmed";
            maintenance.IsConfirmedByStudent = true;
            maintenance.DateResolved = DateTime.Now;

            // Get building managers
            var managers = db.Staffs
                .Where(s => s.ResidenceID == student.ResidenceID
                         && s.Role == "Building Manager")
                .ToList();

            if (!managers.Any())
            {
                TempData["WarningMessage"] = "No building manager found.";
                return RedirectToAction("Maintenance");
            }

            // ✅ Create notifications (CLEAN & VALID)
            foreach (var manager in managers)
            {
                db.Notifications.Add(new Notification
                {
                    UserID = manager.StaffID,
                    UserType = "BuildingManager",
                    Title = "Maintenance Confirmed",
                    Message = $"Room {maintenance.RoomNumber} maintenance has been confirmed by the student.",
                    NotificationType = "MaintenanceConfirmed",
                    RelatedID = maintenance.MaintenanceID,
                    RelatedType = "Maintenance",
                    DateCreated = DateTime.Now,
                    IsRead = false
                });
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = "Confirmation sent successfully.";
            return RedirectToAction("Maintenance");
        }

        // GET: Student/Complaints
        public ActionResult Complaints()
        {
            if (Session["StudentID"] == null)
                return RedirectToAction("StudentLogin", "Auth");

            if (!IsStudentAllocated())
                return RedirectToAction("NotAllocated");

            int studentId = GetLoggedInStudentId();

            var complaints = db.Complaints
                .Where(c => c.StudentID == studentId)
                .OrderByDescending(c => c.DateSubmitted)
                .Select(c => new ComplaintSummaryViewModel
                {
                    ComplaintId = c.ComplaintId,
                    Subject = c.Subject,
                    Category = c.Category,
                    Description = c.Description,
                    Status = c.Status,
                    ManagerFeedback = c.ManagerFeedback,
                    DateSubmitted = c.DateSubmitted,
                    LastUpdated = c.LastUpdated,
                    DateResolved = c.DateResolved
                })
                .ToList();

            return View(complaints);
        }

        // GET: Student/ConductHistory - a student can see only their own recorded warnings.
        public ActionResult ConductHistory()
        {
            if (Session["StudentID"] == null)
                return RedirectToAction("StudentLogin", "Auth");

            int studentId = GetLoggedInStudentId();
            var history = db.StudentConductRecords
                .Include(r => r.Complaint)
                .Where(r => r.StudentID == studentId)
                .OrderByDescending(r => r.IssuedAt)
                .ToList();
            return View(history);
        }

        // GET: Student/CreateComplaint
        public ActionResult CreateComplaint()
        {
            if (Session["StudentID"] == null)
                return RedirectToAction("StudentLogin", "Auth");

            if (!IsStudentAllocated())
                return RedirectToAction("NotAllocated");

            int studentId = GetLoggedInStudentId();

            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student?.ResidenceID != null)
            {
                ViewBag.ReportedStudents = new SelectList(
                    db.Students.Where(s => s.IsActive && s.ResidenceID == student.ResidenceID && s.StudentID != studentId)
                        .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
                        .Select(s => new { s.StudentID, DisplayName = s.FirstName + " " + s.LastName + " (" + s.StudentNumber + ")" })
                        .ToList(), "StudentID", "DisplayName");
                var buildingManager = db.Staffs
                    .FirstOrDefault(s => s.ResidenceID == student.ResidenceID &&
                                         s.Role == "Building Manager" &&
                                         s.IsActive);

                ViewBag.BuildingManagerName = buildingManager != null
                    ? $"{buildingManager.FirstName} {buildingManager.LastName}"
                    : null;
            }

            return View();
        }

        // POST: Student/CreateComplaint
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateComplaint(Complaint complaint)
        {
            if (Session["StudentID"] == null)
                return RedirectToAction("StudentLogin", "Auth");

            if (!IsStudentAllocated())
                return RedirectToAction("NotAllocated");

            if (complaint == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            int studentId = GetLoggedInStudentId();

            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student == null)
                return RedirectToAction("StudentLogin", "Auth");

            if (student.ResidenceID == null)
            {
                TempData["ErrorMessage"] = "You must be allocated to a residence before submitting a complaint.";
                return RedirectToAction("Dashboard");
            }

            var buildingManager = db.Staffs
                .FirstOrDefault(s => s.ResidenceID == student.ResidenceID &&
                                     s.Role == "Building Manager" &&
                                     s.IsActive);

            if (buildingManager == null)
            {
                TempData["ErrorMessage"] = "No active building manager is currently assigned to your residence. Please contact residence administration.";
                return RedirectToAction("Complaints");
            }

            // Sanitise inputs
            complaint.Subject = complaint.Subject?.Trim();
            complaint.Category = complaint.Category?.Trim();
            complaint.Description = complaint.Description?.Trim();

            if (string.IsNullOrWhiteSpace(complaint.Subject))
                ModelState.AddModelError("Subject", "Please enter a subject for your complaint.");

            if (!complaint.ReportedStudentID.HasValue)
                ModelState.AddModelError("ReportedStudentID", "Please select the student you are reporting.");
            else if (complaint.ReportedStudentID.Value == studentId)
                ModelState.AddModelError("ReportedStudentID", "You cannot submit a complaint about yourself.");
            else if (!db.Students.Any(s => s.StudentID == complaint.ReportedStudentID.Value && s.IsActive && s.ResidenceID == student.ResidenceID))
                ModelState.AddModelError("ReportedStudentID", "You may only report an active student in your residence.");

            if (!ModelState.IsValid)
            {
                ViewBag.ReportedStudents = new SelectList(
                    db.Students.Where(s => s.IsActive && s.ResidenceID == student.ResidenceID && s.StudentID != studentId)
                        .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
                        .Select(s => new { s.StudentID, DisplayName = s.FirstName + " " + s.LastName + " (" + s.StudentNumber + ")" })
                        .ToList(), "StudentID", "DisplayName", complaint.ReportedStudentID);
                return View(complaint);
            }

            // Persist
            DateTime submittedAt = DateTime.Now;
            bool urgentComplaint = (complaint.Category ?? string.Empty).IndexOf("safety", StringComparison.OrdinalIgnoreCase) >= 0
                || (complaint.Category ?? string.Empty).IndexOf("security", StringComparison.OrdinalIgnoreCase) >= 0
                || (complaint.Description ?? string.Empty).IndexOf("threat", StringComparison.OrdinalIgnoreCase) >= 0;

            complaint.StudentID = studentId;
            complaint.DateSubmitted = submittedAt;
            complaint.LastUpdated = submittedAt;
            complaint.Status = "Pending";
            complaint.DateResolved = null;
            complaint.ReviewedByStaffID = null;
            complaint.Priority = urgentComplaint ? "High" : "Normal";
            complaint.TargetResolutionBy = submittedAt.AddDays(urgentComplaint ? 1 : 3);
            complaint.EscalatedAt = null;
            complaint.EscalationReason = null;
            complaint.WarningIssued = false;
            complaint.WarningSeverity = null;
            complaint.WarningReason = null;
            complaint.WarningIssuedAt = null;
            complaint.WarningIssuedByStaffID = null;

            db.Complaints.Add(complaint);
            db.SaveChanges();

            // Notify building manager
            _notificationService.NotifyBuildingManagerComplaint(complaint.ComplaintId);

            // Confirm submission to student via in-app notification
            _notificationService.CreateNotification(
                complaint.StudentID ?? 0,
                "Student",
                "Complaint Submitted",
                $"Your complaint \"{complaint.Subject}\" has been submitted to {buildingManager.FirstName} {buildingManager.LastName} and will be reviewed shortly.",
                "Complaint",
                complaint.ComplaintId,
                "Complaint"
            );

            TempData["SuccessMessage"] = $"Your complaint has been sent to {buildingManager.FirstName} {buildingManager.LastName}.";
            return RedirectToAction("Complaints");
        }
        protected int GetLoggedInStudentId()
        {
            // Assuming you store the StudentID in session after login
            if (Session["StudentID"] != null)
            {
                return (int)Session["StudentID"];
            }

            // Alternative: Get from authenticated user
            if (User.Identity.IsAuthenticated)
            {
                // If you store StudentID in claims
                var claim = ((ClaimsIdentity)User.Identity).FindFirst("StudentID");
                if (claim != null)
                {
                    return int.Parse(claim.Value);
                }
            }

            throw new UnauthorizedAccessException("No logged-in student found");
        }

    }
}
