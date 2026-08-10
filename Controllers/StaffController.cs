using DUTResManagementSystem.Models;
using DUTResManagementSystem.ViewModels;
using DUTResSystemWebApp.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Stripe;


namespace DUTResSystemWebApp.Controllers
{
    public class StaffController : Controller
    {
        private readonly ResContext db = new ResContext();

        // GET: Staff/Dashboard
        public ActionResult Dashboard()
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            int staffId = (int)Session["StaffID"];
            var staff = db.Staffs.Find(staffId);

            var notifications = db.Notifications
    .Where(n => n.UserID == staffId
             && n.UserType == "BuildingManager"
             && n.NotificationType == "MaintenanceConfirmed"
             && n.IsRead == false)
    .OrderByDescending(n => n.DateCreated)
    .ToList();

            if (staff == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            // Disable proxy creation to avoid DynamicProxies issues
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;

            // Get statistics based on staff role
            int totalStudents = 0;
            int availableRooms = 0;
            int pendingMaintenance = 0;

            // Fetch rooms and their occupants
            var roomsQuery = db.Rooms.Include(r => r.Students);

            if (staff.Role == "Admin")
            {
                totalStudents = db.Students.Count();
                pendingMaintenance = db.Maintenances.Count(m => m.Status == "Pending");
            }
            else if (staff.Role == "Building Manager")
            {
                totalStudents = db.Students.Count(s => s.ResidenceID == staff.ResidenceID);
                pendingMaintenance = db.Maintenances
                    .Where(m => m.Student.ResidenceID == staff.ResidenceID)
                    .Count(m => m.Status == "Pending");

                roomsQuery = roomsQuery.Where(r => r.ResidenceID == staff.ResidenceID);
            }

            var rooms = roomsQuery.ToList();

            var roomStats = rooms.Select(r => new RoomWithOccupants
            {
                Room = r,
                Occupants = r.Students.ToList()
            }).ToList();

            availableRooms = roomStats.Count(r => !r.IsFull);

            var viewModel = new StaffDashboardViewModel
            {
                Staff = staff,
                Notifications = notifications,
                Announcements = db.Announcements
                    .Where(a => (a.TargetAudience == "Everyone" || a.TargetAudience == "Staff") &&
                                (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now))
                    .OrderByDescending(a => a.DatePosted)
                    .Take(5)
                    .ToList(),
                MaintenanceRequests = db.Maintenances
                    .Include(m => m.Student)
                    .OrderByDescending(m => m.DateReported)
                    .Take(5)
                    .ToList(),
                Residences = db.Residences
                    .AsNoTracking()
                    .Take(5)
                    .ToList(),
                TotalStudents = totalStudents,
                AvailableRooms = availableRooms,
                PendingMaintenance = pendingMaintenance
            };

            // Re-enable proxy creation if needed for other operations
            db.Configuration.ProxyCreationEnabled = true;
            db.Configuration.LazyLoadingEnabled = true;

            // Return different views based on role
            if (staff.Role == "Admin")
            {
                return View("AdminDashboard", viewModel);
            }
            else
            {
                return View("ResidenceAdminDashboard", viewModel);
            }
        }
        //Mark message as read so it disappears
        [HttpPost]
        public ActionResult MarkAsRead(int id)
        {
            var notification = db.Notifications.Find(id);

            if (notification != null)
            {
                notification.IsRead = true;
                db.SaveChanges();
            }

            return RedirectToAction("Dashboard");
        }



        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("StaffLogin", "Auth");
        }

        //// GET: Staff/ManageResidences
        //public ActionResult ManageResidences()
        //{
        //    if (Session["StaffID"] == null)
        //        return RedirectToAction("StaffLogin", "Auth");

        //    var staff = db.Staffs.Find((int)Session["StaffID"]);
        //    if (staff == null || staff.Role != "Admin")
        //        return RedirectToAction("Dashboard", "Staff");

        //    // Load residences and update CurrentOccupancy from actual student count
        //    var residences = db.Residences.ToList();
        //    foreach (var residence in residences)
        //    {
        //        // Count students actually allocated to this residence right now
        //        residence.CurrentOccupancy = db.Students
        //            .Count(s => s.ResidenceID == residence.ResidenceID);
        //    }

        //    // Pass live stats to ViewBag so the view doesn't have to recalculate
        //    ViewBag.TotalCapacity = residences.Sum(r => r.Capacity);
        //    ViewBag.TotalOccupancy = residences.Sum(r => r.CurrentOccupancy);
        //    ViewBag.AvailableSpaces = residences.Sum(r => r.Capacity - r.CurrentOccupancy);

        //    return View(residences);
        //}


        // POST: Staff/AddResidence
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddResidence(Residence model)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var residence = new Residence
                    {
                        Name = model.Name,
                        Address = model.Address,
                        Capacity = model.Capacity,
                        GenderPolicy = model.GenderPolicy,
                        ContactNumber = model.ContactNumber,
                        Faculty = model.Faculty,

                    };

                    db.Residences.Add(residence);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = $"Residence '{model.Name}' added successfully!";
                    return RedirectToAction("ManageResidences");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error adding residence: " + ex.Message;
                }
            }

            return RedirectToAction("ManageResidences");
        }

        // GET: Staff/EditResidence/5
        public ActionResult EditResidence(int id)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            var residence = db.Residences.Find(id);
            if (residence == null)
            {
                return HttpNotFound();
            }

            return View(residence);
        }

        // POST: Staff/EditResidence/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditResidence(Residence model)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var residence = db.Residences.Find(model.ResidenceID);
                    if (residence != null)
                    {
                        residence.Name = model.Name;
                        residence.Address = model.Address;
                        residence.Capacity = model.Capacity;
                        residence.GenderPolicy = model.GenderPolicy;
                        residence.ContactNumber = model.ContactNumber;
                        residence.Faculty = model.Faculty;

                        db.SaveChanges();
                        TempData["SuccessMessage"] = $"Residence '{model.Name}' updated successfully!";
                    }
                    return RedirectToAction("ManageResidences");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error updating residence: " + ex.Message;
                }
            }

            return View(model);
        }

        // POST: Staff/DeleteResidence/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteResidence(int id)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            try
            {
                var residence = db.Residences.Find(id);
                if (residence != null)
                {
                    // Check if residence has students before deleting
                    if (db.Students.Any(s => s.ResidenceID == id))
                    {
                        TempData["ErrorMessage"] = $"Cannot delete '{residence.Name}' because it has students assigned. Reassign students first.";
                        return RedirectToAction("ManageResidences");
                    }

                    db.Residences.Remove(residence);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = $"Residence '{residence.Name}' deleted successfully!";
                }
                return RedirectToAction("ManageResidences");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error deleting residence: " + ex.Message;
                return RedirectToAction("ManageResidences");
            }
        }
        public ActionResult ManageResidences()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
                return RedirectToAction("Dashboard", "Staff");

            // Load residences and update CurrentOccupancy from actual student count
            var residences = db.Residences.ToList();
            foreach (var residence in residences)
            {
                // Count students actually allocated to this residence right now
                residence.CurrentOccupancy = db.Students
                    .Count(s => s.ResidenceID == residence.ResidenceID);
            }

            // Pass live stats to ViewBag so the view doesn't have to recalculate
            ViewBag.TotalCapacity = residences.Sum(r => r.Capacity);
            ViewBag.TotalOccupancy = residences.Sum(r => r.CurrentOccupancy);
            ViewBag.AvailableSpaces = residences.Sum(r => r.Capacity - r.CurrentOccupancy);

            return View(residences);
        }
        public ActionResult StudentAllocation(string searchString, string faculty, string gender, string status)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var students = db.Students.Include(s => s.Residence).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.StudentNumber.Contains(searchString) ||
                                               s.FirstName.Contains(searchString) ||
                                               s.LastName.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(faculty))
            {
                students = students.Where(s => s.Faculty == faculty);
            }

            if (!string.IsNullOrEmpty(gender))
            {
                students = students.Where(s => s.Gender == gender);
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "allocated")
                    students = students.Where(s => s.ResidenceID != null);
                else if (status == "not-allocated")
                    students = students.Where(s => s.ResidenceID == null);
            }

            ViewBag.SearchString = searchString;
            ViewBag.Faculty = faculty;
            ViewBag.Gender = gender;
            ViewBag.Status = status;

            // Get all residences with availability info for the summary table
            var allResidences = db.Residences
                .Select(r => new ResidenceBreakdownViewModel
                {
                    ResidenceID = r.ResidenceID,
                    Name = r.Name,
                    Capacity = r.Capacity,
                    Occupied = r.Students.Count(),
                    Available = r.Capacity - r.Students.Count(),
                    GenderPolicy = r.GenderPolicy,
                    Faculty = r.Faculty
                })
                .OrderByDescending(r => r.Available)
                .ToList();

            ViewBag.ResidenceBreakdown = allResidences;

            // For dropdowns: Get only residences with space
            var residencesWithSpace = db.Residences
                .Where(r => r.Students.Count() < r.Capacity)
                .Select(r => new
                {
                    r.ResidenceID,
                    r.Name,
                    r.Faculty,
                    r.GenderPolicy,
                    Available = r.Capacity - r.Students.Count()
                })
                .ToList();

            // Format dropdown text: "Residence Name (X available)"
            ViewBag.Residences = new SelectList(
                residencesWithSpace.Select(r => new
                {
                    r.ResidenceID,
                    DisplayText = $"{r.Name} ({r.Available} available)" +
                                 (string.IsNullOrEmpty(r.Faculty) ? " - All Faculties" : $" - {r.Faculty}")
                }),
                "ResidenceID",
                "DisplayText"
            );

            // Store residence data for JavaScript filtering - WITH NORMALIZED FACULTY
            ViewBag.ResidenceData = residencesWithSpace.Select(r => new
            {
                r.ResidenceID,
                r.Name,
                Faculty = NormalizeFaculty(r.Faculty), // Normalized for comparison
                OriginalFaculty = r.Faculty, // Original for display
                r.GenderPolicy,
                r.Available
            }).ToList();

            ViewBag.AvailableSpaces = db.Residences
                .Sum(r => r.Capacity - r.Students.Count());

            return View(students.ToList());
        }

        // HELPER METHOD: Normalize faculty names - converts "&" to "and" for consistency
        private string NormalizeFaculty(string faculty)
        {
            if (string.IsNullOrEmpty(faculty)) return null;
            // Convert to lowercase and standardize "&" to "and"
            return faculty.ToLower().Trim().Replace(" & ", " and ");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AllocateStudent(int studentId, int residenceId)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var student = db.Students.Find(studentId);
            var residence = db.Residences.Find(residenceId);

            if (student == null || residence == null)
            {
                TempData["ErrorMessage"] = "Student or residence not found.";
                return RedirectToAction("StudentAllocation");
            }

            // Normalize faculties for comparison (convert & to and)
            string studentFacultyNorm = NormalizeFaculty(student.Faculty);
            string residenceFacultyNorm = NormalizeFaculty(residence.Faculty);

            // Validate Gender Policy
            if (residence.GenderPolicy != "Mixed" && residence.GenderPolicy != student.Gender)
            {
                TempData["ErrorMessage"] = $"Cannot allocate {student.Gender} student to {residence.GenderPolicy} residence.";
                return RedirectToAction("StudentAllocation");
            }

            // Validate Faculty - Student can only go to residence matching their faculty OR unrestricted residence
            if (!string.IsNullOrEmpty(residenceFacultyNorm) && residenceFacultyNorm != studentFacultyNorm)
            {
                TempData["ErrorMessage"] = $"Faculty mismatch: {student.FirstName} {student.LastName} ({student.Faculty}) cannot be allocated to {residence.Name} (restricted to {residence.Faculty} only).";
                return RedirectToAction("StudentAllocation");
            }

            if (residence.Students.Count >= residence.Capacity)
            {
                TempData["ErrorMessage"] = "Residence is at full capacity.";
                return RedirectToAction("StudentAllocation");
            }

            if (student.ResidenceID != null)
            {
                TempData["ErrorMessage"] = "Student is already allocated to a residence.";
                return RedirectToAction("StudentAllocation");
            }

            student.ResidenceID = residenceId;
            db.SaveChanges();

            var notificationService = new NotificationService();
            notificationService.NotifyStudentResidenceAllocation(student.StudentID, residenceId);
            notificationService.NotifyBuildingManagerStudentAllocation(student.StudentID);

            TempData["SuccessMessage"] = $"Student {student.StudentNumber} allocated to {residence.Name} successfully.";
            return RedirectToAction("StudentAllocation");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeallocateStudent(int studentId)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var student = db.Students.Find(studentId);

            if (student == null || student.ResidenceID == null)
            {
                TempData["ErrorMessage"] = "Student not found or not allocated.";
                return RedirectToAction("StudentAllocation");
            }

            string residenceName = student.Residence?.Name ?? "your residence";
            string roomNumber = null;

            if (student.RoomID != null)
            {
                var room = db.Rooms.Find(student.RoomID);
                if (room != null)
                {
                    roomNumber = room.RoomNumber;
                    room.Status = "Available";
                }
                student.RoomID = null;
            }

            student.ResidenceID = null;
            db.SaveChanges();

            var notificationService = new NotificationService();
            notificationService.NotifyStudentResidenceDeallocation(student.StudentID, residenceName);

            string successMsg = $"Student {student.StudentNumber} removed from {residenceName}";
            if (roomNumber != null)
                successMsg += $" and Room {roomNumber}";
            successMsg += " successfully.";

            TempData["SuccessMessage"] = successMsg;
            return RedirectToAction("StudentAllocation");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult BulkAllocateStudents(List<int> studentIds, int? residenceId)
        {
            if (Session["StaffID"] == null)
            {
                return Json(new { success = false, message = "Unauthorized. Please log in." });
            }

            if (studentIds == null || studentIds.Count == 0)
            {
                return Json(new { success = false, message = "No students selected." });
            }

            var chosenResidence = residenceId.HasValue ? db.Residences.Find(residenceId.Value) : null;
            if (residenceId.HasValue && chosenResidence == null)
            {
                return Json(new { success = false, message = "Selected residence not found." });
            }

            try
            {
                int allocatedCount = 0;
                var results = new List<object>();
                var allocatedPairs = new List<(int StudentId, int ResidenceId)>();

                foreach (var studentId in studentIds)
                {
                    var student = db.Students.Find(studentId);
                    if (student == null || student.ResidenceID != null)
                    {
                        results.Add(new { studentId, success = false, reason = "Student not found or already allocated" });
                        continue;
                    }

                    // Normalize student faculty (convert & to and)
                    string studentFacultyNorm = NormalizeFaculty(student.Faculty);
                    var targetResidence = (Residence)null;

                    if (chosenResidence != null)
                    {
                        // Normalize chosen residence faculty
                        string chosenResidenceFacultyNorm = NormalizeFaculty(chosenResidence.Faculty);

                        // Validate gender policy
                        bool genderOk = chosenResidence.GenderPolicy == "Mixed" || chosenResidence.GenderPolicy == student.Gender;
                        // Validate faculty (normalized)
                        bool facultyOk = string.IsNullOrEmpty(chosenResidenceFacultyNorm) || chosenResidenceFacultyNorm == studentFacultyNorm;
                        bool hasSpace = chosenResidence.Students.Count < chosenResidence.Capacity;

                        if (!genderOk)
                        {
                            results.Add(new
                            {
                                studentId,
                                success = false,
                                reason = $"Gender policy '{chosenResidence.GenderPolicy}' does not allow '{student.Gender}' students"
                            });
                            continue;
                        }

                        if (!facultyOk)
                        {
                            results.Add(new
                            {
                                studentId,
                                success = false,
                                reason = $"Faculty mismatch: Student is '{student.Faculty}', Residence restricted to '{chosenResidence.Faculty}'"
                            });
                            continue;
                        }

                        if (!hasSpace)
                        {
                            results.Add(new
                            {
                                studentId,
                                success = false,
                                reason = $"'{chosenResidence.Name}' is at full capacity"
                            });
                            continue;
                        }
                        targetResidence = chosenResidence;
                    }
                    else
                    {
                        // Auto-assign: load residences with space
                        var availableResidences = db.Residences
                            .Where(r => r.Students.Count < r.Capacity)
                            .ToList();

                        // Step 1: Perfect match - Gender + Faculty (normalized)
                        targetResidence = availableResidences
                            .Where(r => (r.GenderPolicy == "Mixed" || r.GenderPolicy == student.Gender) &&
                                        (string.IsNullOrEmpty(NormalizeFaculty(r.Faculty)) || NormalizeFaculty(r.Faculty) == studentFacultyNorm))
                            .OrderByDescending(r => r.Capacity - r.Students.Count)
                            .FirstOrDefault();

                        // Step 2: If no faculty match found, report specific error
                        if (targetResidence == null)
                        {
                            var genderMatches = availableResidences
                                .Where(r => r.GenderPolicy == "Mixed" || r.GenderPolicy == student.Gender)
                                .ToList();

                            if (genderMatches.Any() && !genderMatches.Any(r => string.IsNullOrEmpty(NormalizeFaculty(r.Faculty)) || NormalizeFaculty(r.Faculty) == studentFacultyNorm))
                            {
                                results.Add(new
                                {
                                    studentId,
                                    success = false,
                                    reason = $"No residence accepts {student.Faculty} students. Available residences are restricted to other faculties."
                                });
                                continue;
                            }
                        }

                        if (targetResidence == null)
                        {
                            string diagReason = !availableResidences.Any() ? "No residences have available space" :
                                                $"No compatible residence for gender '{student.Gender}' / faculty '{student.Faculty}'";
                            results.Add(new { studentId, success = false, reason = diagReason });
                            continue;
                        }
                    }

                    student.ResidenceID = targetResidence.ResidenceID;
                    allocatedCount++;
                    allocatedPairs.Add((studentId, targetResidence.ResidenceID));
                    results.Add(new { studentId, success = true, residence = targetResidence.Name });
                }

                db.SaveChanges();

                var notificationService = new NotificationService();
                foreach (var (sid, rid) in allocatedPairs)
                {
                    notificationService.NotifyStudentResidenceAllocation(sid, rid);
                    notificationService.NotifyBuildingManagerStudentAllocation(sid);
                }

                return Json(new { success = true, allocatedCount, total = studentIds.Count, results });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // GET: Staff/SendAnnouncement
        public ActionResult SendAnnouncement()
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            // PASS THE ROLE TO THE VIEW - THIS IS CRITICAL!
            ViewBag.UserRole = staff.Role;

            return View();
        }

        // POST: Staff/SendAnnouncement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendAnnouncement(Announcement model)
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Set the staff ID who created the announcement
                    model.StaffID = staff.StaffID;
                    model.DatePosted = DateTime.Now;

                    // Set default target audience based on role
                    if (staff.Role == "Building Manager" && string.IsNullOrEmpty(model.TargetAudience))
                    {
                        model.TargetAudience = "Students"; // Building Manager can only send to students
                    }
                    else if (staff.Role == "Admin" && string.IsNullOrEmpty(model.TargetAudience))
                    {
                        model.TargetAudience = "Everyone"; // Admin default to Everyone
                    }

                    db.Announcements.Add(model);
                    db.SaveChanges();

                    string audience = model.TargetAudience ?? "Everyone";
                    TempData["SuccessMessage"] = $"Announcement sent to {audience} successfully!";

                    return RedirectToAction("Dashboard", "Staff");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error sending announcement: " + ex.Message);
                }
            }

            ViewBag.UserRole = staff.Role;
            return View(model);
        }



        // GET: Staff/ViewAnnouncements
        public ActionResult ViewAnnouncements()
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            try
            {
                // Get announcements for staff (Everyone + Staff announcements)
                var announcements = db.Announcements
                    .Where(a => (a.TargetAudience == "Everyone" || a.TargetAudience == "Staff") &&
                               (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now))
                    .OrderByDescending(a => a.DatePosted)
                    .ToList();

                ViewBag.UserRole = staff.Role;
                return View(announcements);
            }
            catch (Exception ex)
            {
                // Log error and return empty list
                System.Diagnostics.Debug.WriteLine($"Error loading announcements: {ex.Message}");
                ViewBag.UserRole = staff.Role;
                return View(new List<Announcement>());
            }
        }

        // In your StaffController

        // Student methods
        public JsonResult GetStudent(int id)
        {
            var student = db.Students.Find(id);
            if (student == null) return Json(null);

            return Json(new
            {
                student.StudentID,
                student.FirstName,
                student.LastName,
                student.Email,
                student.Gender,
                student.Faculty,
                student.YearOfStudy,
                student.IsActive
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult EditStudent(int studentId, string firstName, string lastName, string email,
                                string gender, string faculty, int yearOfStudy)
        {
            var student = db.Students.Find(studentId);
            if (student == null)
            {
                TempData["ErrorMessage"] = "Student not found.";
                return RedirectToAction("UserManagement");
            }

            student.FirstName = firstName;
            student.LastName = lastName;
            student.Email = email;
            student.Gender = gender;
            student.Faculty = faculty;
            student.YearOfStudy = yearOfStudy;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Student updated successfully.";
            return RedirectToAction("UserManagement");
        }


        [HttpPost]
        public ActionResult ToggleStudentStatus(int id)
        {
            var student = db.Students.Find(id);
            if (student == null)
            {
                TempData["ErrorMessage"] = "Student not found.";
                return RedirectToAction("UserManagement");
            }

            student.IsActive = !student.IsActive;
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Student {((bool)student.IsActive ? "activated" : "deactivated")} successfully.";
            return RedirectToAction("UserManagement");
        }

        // Staff methods (similar structure)
        public JsonResult GetStaff(int id)
        {
            var staff = db.Staffs.Find(id);
            if (staff == null) return Json(null);

            return Json(new
            {
                staff.StaffID,
                staff.FirstName,
                staff.LastName,
                staff.Email,
                staff.Role,
                staff.IsActive
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult EditStaff(int staffId, string firstName, string lastName, string email,
                                     string role, bool isActive)
        {
            var staff = db.Staffs.Find(staffId);
            if (staff == null)
            {
                TempData["ErrorMessage"] = "Staff member not found.";
                return RedirectToAction("UserManagement");
            }

            staff.FirstName = firstName;
            staff.LastName = lastName;
            staff.Email = email;
            staff.Role = role;
            staff.IsActive = isActive;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Staff member updated successfully.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public ActionResult ToggleStaffStatus(int id)
        {
            var staff = db.Staffs.Find(id);
            if (staff == null)
            {
                TempData["ErrorMessage"] = "Staff member not found.";
                return RedirectToAction("UserManagement");
            }

            staff.IsActive = !staff.IsActive;
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Staff member {((bool)staff.IsActive ? "activated" : "deactivated")} successfully.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public ActionResult EditStaffs(int staffId, string firstName, string lastName, string email,
                                     string role, bool isActive)
        {
            var staff = db.Staffs.Find(staffId);
            if (staff == null)
            {
                TempData["ErrorMessage"] = "Staff member not found.";
                return RedirectToAction("UserManagement");
            }

            staff.FirstName = firstName;
            staff.LastName = lastName;
            staff.Email = email;
            staff.Role = role;
            staff.IsActive = isActive;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Staff member updated successfully.";
            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public ActionResult ToggleStaffsStatus(int id)
        {
            var staff = db.Staffs.Find(id);
            if (staff == null)
            {
                TempData["ErrorMessage"] = "Staff member not found.";
                return RedirectToAction("UserManagement");
            }

            staff.IsActive = !staff.IsActive;
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Staff member {((bool)staff.IsActive ? "activated" : "deactivated")} successfully.";
            return RedirectToAction("UserManagement");
        }



        // GET: Staff/ViewUserDetails
        public ActionResult ViewUserDetails()
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                return RedirectToAction("Dashboard", "Staff");
            }

            // Create a view model that contains both students and staff
            var userDetailsViewModel = new UserDetailsViewModel
            {
                Students = db.Students.ToList(),
                Staff = db.Staffs.ToList()
            };

            return View(userDetailsViewModel);
        }
        public ActionResult ReviewApplications()
        {
            var applications = db.ResidenceApplications
                                 .Where(x => x.Status == "Pending")
                                 .ToList();

            return View(applications);
        }
        [HttpPost]
        public ActionResult ReviewApplication(int id, string status, string feedback)
        {
            var app = db.ResidenceApplications.Find(id);

            if (app != null)
            {
                app.Status = status;
                app.AdminFeedback = feedback;

                db.SaveChanges();
            }

            return RedirectToAction("ReviewApplications");
        }

        // GET: Staff/RoomAllocation
        // Building managers only — system auto-detects their residence
        public ActionResult RoomAllocation()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            // Only building managers are allowed on this page
            if (staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can access room allocation.";
                return RedirectToAction("Dashboard", "Staff");
            }

            // Building manager must be assigned to a residence
            if (staff.ResidenceID == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to a residence. Please contact the administrator.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var residence = db.Residences.Find(staff.ResidenceID);

            // Get ALL check-in records for this residence with both HasCheckedIn and HasCheckedOut
            var checkInRecords = db.ResidenceCheckIns
                .Where(c => c.ResidenceID == staff.ResidenceID)
                .Select(c => new { c.StudentID, c.HasCheckedIn, c.HasCheckedOut, c.CheckInTime, c.CheckOutTime })
                .ToList();

            var checkInDict = checkInRecords.ToDictionary(c => c.StudentID, c => c);

            // =========================================================
            // CATEGORY 1: Students WITHOUT a room
            // Split into: Checked In AND NOT Checked Out (ready) vs Not Checked In
            // =========================================================
            var allUnallocatedStudents = db.Students
                .Where(s => s.ResidenceID == staff.ResidenceID && s.RoomID == null)
                .ToList();

            var checkedInStudents = new List<Student>();
            var notCheckedInStudents = new List<Student>();

            foreach (var student in allUnallocatedStudents)
            {
                // Check if student has checked in AND has NOT checked out
                var record = checkInDict.ContainsKey(student.StudentID) ? checkInDict[student.StudentID] : null;
                bool isCheckedInAndNotCheckedOut = record != null && record.HasCheckedIn && !record.HasCheckedOut;

                if (isCheckedInAndNotCheckedOut)
                {
                    checkedInStudents.Add(student);
                }
                else
                {
                    notCheckedInStudents.Add(student);
                }
            }

            // =========================================================
            // CATEGORY 2: Students WITH a room (already allocated)
            // =========================================================
            var studentsWithRooms = new List<StudentWithRoomInfo>();

            var allocatedStudents = db.Students
                .Where(s => s.ResidenceID == staff.ResidenceID && s.RoomID != null)
                .Include(s => s.Room)
                .ToList();

            foreach (var student in allocatedStudents)
            {
                var record = checkInDict.ContainsKey(student.StudentID) ? checkInDict[student.StudentID] : null;

                studentsWithRooms.Add(new StudentWithRoomInfo
                {
                    Student = student,
                    Room = student.Room,
                    RoomNumber = student.Room?.RoomNumber ?? "Unknown",
                    HasCheckedIn = record?.HasCheckedIn ?? false,
                    HasCheckedOut = record?.HasCheckedOut ?? false,
                    CheckInTime = record?.CheckInTime,
                    CheckOutTime = record?.CheckOutTime
                });
            }

            // All rooms in THIS residence with their occupants
            var rooms = db.Rooms
                .Where(r => r.ResidenceID == staff.ResidenceID)
                .OrderBy(r => r.Floor)
                .ThenBy(r => r.RoomNumber)
                .ToList();

            var roomsWithOccupants = new List<RoomWithOccupants>();
            foreach (var room in rooms)
            {
                var occupants = db.Students.Where(s => s.RoomID == room.RoomID).ToList();
                roomsWithOccupants.Add(new RoomWithOccupants
                {
                    Room = room,
                    Occupants = occupants
                });
            }

            var viewModel = new RoomAllocationViewModel
            {
                SelectedResidence = residence,
                UnallocatedStudentsByStatus = new UnallocatedStudentsViewModel
                {
                    CheckedIn = checkedInStudents.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList(),
                    NotCheckedIn = notCheckedInStudents.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList(),
                    CheckedInCount = checkedInStudents.Count,
                    NotCheckedInCount = notCheckedInStudents.Count
                },
                StudentsWithRooms = studentsWithRooms.OrderBy(s => s.RoomNumber).ThenBy(s => s.Student.LastName).ToList(),
                Rooms = roomsWithOccupants
            };

            // Also populate the old property for backward compatibility
            viewModel.UnallocatedStudents = allUnallocatedStudents;

            // Pass counts to ViewBag for display
            ViewBag.TotalUnallocated = allUnallocatedStudents.Count;
            ViewBag.CheckedInReadyCount = checkedInStudents.Count;
            ViewBag.NotCheckedInCount = notCheckedInStudents.Count;
            ViewBag.AllocatedCount = studentsWithRooms.Count;
            ViewBag.TotalStudents = allUnallocatedStudents.Count + studentsWithRooms.Count;

            return View(viewModel);
        }


        // POST: Staff/AllocateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AllocateRoom(int studentId, int roomId)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null || staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can allocate students to rooms.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var student = db.Students
                            .Include(s => s.Residence)
                            .FirstOrDefault(s => s.StudentID == studentId);

            var room = db.Rooms.Find(roomId);

            if (student == null || room == null)
            {
                TempData["ErrorMessage"] = "Student or room not found.";
                return RedirectToAction("RoomAllocation");
            }

            // ─────────────────────────────────────────────────────────────
            // CHECK-IN GATE - STUDENT MUST CHECK IN FIRST
            // This is the code you asked me to add - it goes RIGHT HERE
            // ─────────────────────────────────────────────────────────────
            var checkIn = db.ResidenceCheckIns
                .FirstOrDefault(c => c.StudentID == studentId &&
                                     c.ResidenceID == staff.ResidenceID &&
                                     c.HasCheckedIn == true);

            if (checkIn == null)
            {
                TempData["ErrorMessage"] =
                    $"Cannot allocate room. {student.FirstName} {student.LastName} " +
                    "has not checked in to the residence yet. " +
                    "Ask them to scan their personal check-in QR code first.";
                return RedirectToAction("RoomAllocation");
            }
            // ─────────────────────────────────────────────────────────────
            // END OF CHECK-IN GATE
            // ─────────────────────────────────────────────────────────────

            // Room must belong to the building manager's residence
            if (room.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only allocate students to rooms in your residence.";
                return RedirectToAction("RoomAllocation");
            }

            // Student must be allocated to this same residence
            if (student.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "This student is not allocated to your residence.";
                return RedirectToAction("RoomAllocation");
            }

            // Student must not already have a room
            if (student.RoomID != null)
            {
                TempData["ErrorMessage"] = $"{student.FirstName} {student.LastName} is already assigned to a room.";
                return RedirectToAction("RoomAllocation");
            }

            // Gender policy check
            if (room.Gender != "Mixed" && room.Gender != student.Gender)
            {
                TempData["ErrorMessage"] =
                    $"Cannot allocate a {student.Gender} student to a {room.Gender} room.";
                return RedirectToAction("RoomAllocation");
            }

            // Capacity check
            int currentOccupants = db.Students.Count(s => s.RoomID == roomId);
            if (currentOccupants >= room.Capacity)
            {
                TempData["ErrorMessage"] = $"Room {room.RoomNumber} is at full capacity.";
                return RedirectToAction("RoomAllocation");
            }

            // Allocate
            student.RoomID = roomId;

            // Mark room as Occupied if this fills the last bed
            if (currentOccupants + 1 >= room.Capacity)
                room.Status = "Occupied";

            db.SaveChanges();

            // Send notification + email
            var notificationService = new NotificationService();
            notificationService.NotifyStudentRoomAllocation(student.StudentID, roomId);

            TempData["SuccessMessage"] =
                $"Student {student.StudentNumber} allocated to Room {room.RoomNumber} successfully.";

            return RedirectToAction("RoomAllocation");
        }


        // POST: Staff/DeallocateRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeallocateRoom(int studentId)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null || staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can remove students from rooms.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var student = db.Students.Find(studentId);

            if (student == null || student.RoomID == null)
            {
                TempData["ErrorMessage"] = "Student not found or not assigned to a room.";
                return RedirectToAction("RoomAllocation");
            }

            // Building manager can only deallocate students in their residence
            if (student.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only manage students in your residence.";
                return RedirectToAction("RoomAllocation");
            }

            var room = db.Rooms.Find(student.RoomID);
            string roomNumber = room?.RoomNumber ?? "Unknown";

            student.RoomID = null;

            // Set room back to Available
            if (room != null)
                room.Status = "Available";

            db.SaveChanges();

            // Send notification + email
            var notificationService = new NotificationService();
            notificationService.NotifyStudentRoomDeallocation(student.StudentID, roomNumber);

            TempData["SuccessMessage"] =
                $"Student {student.StudentNumber} removed from Room {roomNumber} successfully.";

            return RedirectToAction("RoomAllocation");
        }


        // GET: Staff/GetRoomsForResidence  (AJAX — feeds the room dropdown in the allocate modal)
        // Automatically uses the logged-in building manager's residence — no residenceId parameter needed
        public JsonResult GetRoomsForResidence(string gender)
        {
            if (Session["StaffID"] == null)
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null || staff.ResidenceID == null)
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            var rooms = db.Rooms
                .Where(r => r.ResidenceID == staff.ResidenceID &&
                            r.Status == "Available" &&
                            (r.Gender == "Mixed" || r.Gender == gender))
                .ToList()
                .Where(r => db.Students.Count(s => s.RoomID == r.RoomID) < r.Capacity)
                .Select(r => new
                {
                    r.RoomID,
                    r.RoomNumber,
                    r.RoomType,
                    r.Floor,
                    r.Gender,
                    r.Capacity,
                    CurrentOccupants = db.Students.Count(s => s.RoomID == r.RoomID)
                })
                .ToList();

            return Json(rooms, JsonRequestBehavior.AllowGet);
        }




        // GET: Staff/RoomManagement
        public ActionResult RoomManagement()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            // Building managers only see rooms for their assigned residence
            // Admins see all rooms across all residences
            List<Room> rooms;
            Residence residence = null;

            if (staff.Role == "Building Manager")
            {
                if (staff.ResidenceID == null)
                {
                    TempData["ErrorMessage"] = "You are not assigned to a residence yet.";
                    return RedirectToAction("Dashboard", "Staff");
                }

                residence = db.Residences.Find(staff.ResidenceID);
                rooms = db.Rooms
                          .Where(r => r.ResidenceID == staff.ResidenceID)
                          .OrderBy(r => r.Floor)
                          .ThenBy(r => r.RoomNumber)
                          .ToList();
            }
            else
            {
                // Admin — show all rooms, let them filter by residence
                rooms = db.Rooms
                          .Include(r => r.Residence)
                          .OrderBy(r => r.Residence.Name)
                          .ThenBy(r => r.Floor)
                          .ThenBy(r => r.RoomNumber)
                          .ToList();
            }

            // Pass occupancy counts to the view
            var roomsWithOccupants = rooms.Select(r => new RoomWithOccupants
            {
                Room = r,
                Occupants = db.Students.Where(s => s.RoomID == r.RoomID).ToList()
            }).ToList();

            ViewBag.Residence = residence;
            ViewBag.StaffRole = staff.Role;
            ViewBag.Residences = new SelectList(db.Residences.OrderBy(r => r.Name), "ResidenceID", "Name");

            return View(roomsWithOccupants);
        }


        // POST: Staff/AddRoom

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddRoom(string roomNumber, string roomType, string gender,
                            int capacity, int residenceId, int? floor, string status)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            // Restrict building manager
            if (staff.Role == "Building Manager" && staff.ResidenceID != residenceId)
            {
                TempData["ErrorMessage"] = "You can only add rooms to your assigned residence.";
                return RedirectToAction("RoomManagement");
            }

            // 🔥 GET RESIDENCE
            var residence = db.Residences
                              .Include("Rooms")
                              .FirstOrDefault(r => r.ResidenceID == residenceId);

            if (residence == null)
            {
                TempData["ErrorMessage"] = "Residence not found.";
                return RedirectToAction("RoomManagement");
            }

            // 🔥 CHECK TOTAL ROOM CAPACITY (beds)
            int currentTotalCapacity = residence.Rooms.Sum(r => r.Capacity);

            if (currentTotalCapacity + capacity > residence.Capacity)
            {
                TempData["ErrorMessage"] =
                    $"Cannot add room. Total room capacity will exceed residence limit ({residence.Capacity}).";
                return RedirectToAction("RoomManagement");
            }

            // Check duplicate room
            bool duplicate = db.Rooms.Any(r => r.ResidenceID == residenceId &&
                                               r.RoomNumber == roomNumber);
            if (duplicate)
            {
                TempData["ErrorMessage"] = $"Room {roomNumber} already exists in this residence.";
                return RedirectToAction("RoomManagement");
            }

            // Create room
            var room = new Room
            {
                ResidenceID = residenceId,
                RoomNumber = roomNumber.Trim(),
                RoomType = roomType,
                Gender = gender,
                Capacity = capacity,
                Floor = floor,
                Status = "Available"
            };

            db.Rooms.Add(room);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Room {roomNumber} added successfully.";
            return RedirectToAction("RoomManagement");
        }

        // POST: Staff/EditRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditRoom(int roomId, string roomNumber, string roomType,
                                     string gender, int capacity, int? floor)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var room = db.Rooms.Find(roomId);
            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (room == null)
            {
                TempData["ErrorMessage"] = "Room not found.";
                return RedirectToAction("RoomManagement");
            }

            // Building managers can only edit rooms in their own residence
            if (staff.Role == "Building Manager" && staff.ResidenceID != room.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only edit rooms in your assigned residence.";
                return RedirectToAction("RoomManagement");
            }

            // Check for duplicate room number (excluding this room)
            bool duplicate = db.Rooms.Any(r => r.ResidenceID == room.ResidenceID &&
                                               r.RoomNumber == roomNumber &&
                                               r.RoomID != roomId);
            if (duplicate)
            {
                TempData["ErrorMessage"] = $"Room {roomNumber} already exists in this residence.";
                return RedirectToAction("RoomManagement");
            }

            room.RoomNumber = roomNumber.Trim();
            room.RoomType = roomType;
            room.Gender = gender;
            room.Capacity = capacity;
            room.Floor = floor;

            // Recalculate status based on current occupancy vs new capacity
            int occupants = db.Students.Count(s => s.RoomID == roomId);
            room.Status = occupants >= capacity ? "Occupied" : "Available";

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Room {roomNumber} updated successfully.";
            return RedirectToAction("RoomManagement");
        }


        // POST: Staff/DeleteRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteRoom(int roomId)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var room = db.Rooms.Find(roomId);
            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (room == null)
            {
                TempData["ErrorMessage"] = "Room not found.";
                return RedirectToAction("RoomManagement");
            }

            // Building managers can only delete rooms in their own residence
            if (staff.Role == "Building Manager" && staff.ResidenceID != room.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only delete rooms in your assigned residence.";
                return RedirectToAction("RoomManagement");
            }

            // Cannot delete a room that has students in it
            int occupants = db.Students.Count(s => s.RoomID == roomId);
            if (occupants > 0)
            {
                TempData["ErrorMessage"] = $"Cannot delete Room {room.RoomNumber} — it still has {occupants} student(s) assigned. Please deallocate them first.";
                return RedirectToAction("RoomManagement");
            }

            db.Rooms.Remove(room);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Room {room.RoomNumber} deleted successfully.";
            return RedirectToAction("RoomManagement");
        }


        // GET: Staff/GetRoom  (AJAX — populates the edit modal)
        public JsonResult GetRoom(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                room.RoomID,
                room.RoomNumber,
                room.RoomType,
                room.Gender,
                room.Capacity,
                room.Floor,
                room.Status,
                room.ResidenceID
            }, JsonRequestBehavior.AllowGet);
        }

        // ---------------------------------------------------------------
        // Replace these methods in your StaffController
        // ---------------------------------------------------------------

        // GET: Staff/RoomChangeRequests
        public ActionResult RoomChangeRequests()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            if (staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can review room change requests.";
                return RedirectToAction("Dashboard");
            }

            // Load requests for this residence
            var requestList = db.RoomChangeRequests
                .Where(r => r.Student.ResidenceID == staff.ResidenceID)
                .OrderByDescending(r => r.DateRequested)
                .ToList();

            // Manually load all navigation properties to avoid EF proxy issues
            foreach (var r in requestList)
            {
                r.Student = db.Students.Find(r.StudentID);
                r.CurrentRoom = db.Rooms.Find(r.CurrentRoomID);

                if (r.ReviewedByStaffID.HasValue)
                    r.ReviewedBy = db.Staffs.Find(r.ReviewedByStaffID.Value);
            }

            ViewBag.ResidenceName = staff.Residence?.Name ?? db.Residences.Find(staff.ResidenceID)?.Name;

            return View(requestList);
        }


        // POST: Staff/ReviewRoomChangeRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewRoomChangeRequest(int requestId, string decision, string adminFeedback)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can review room change requests.";
                return RedirectToAction("Dashboard");
            }

            var request = db.RoomChangeRequests
                .FirstOrDefault(r => r.RequestID == requestId);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction("RoomChangeRequests");
            }

            // Load student and current room manually
            var student = db.Students.Find(request.StudentID);
            var currentRoom = db.Rooms.Find(request.CurrentRoomID);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student not found.";
                return RedirectToAction("RoomChangeRequests");
            }

            // Building manager can only review requests for their residence
            if (student.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only review requests for your residence.";
                return RedirectToAction("RoomChangeRequests");
            }

            // Only pending requests can be reviewed
            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "This request has already been reviewed.";
                return RedirectToAction("RoomChangeRequests");
            }

            if (decision != "Approved" && decision != "Declined")
            {
                TempData["ErrorMessage"] = "Invalid decision.";
                return RedirectToAction("RoomChangeRequests");
            }

            if (decision == "Approved")
            {
                // Deallocate student from their current room
                // They will appear in unallocated students and the admin will manually assign them
                if (currentRoom != null)
                    currentRoom.Status = "Available";

                student.RoomID = null;

                // Send approval email + notification
                var ns = new NotificationService();
                ns.SendRoomChangeApprovalEmail(student, currentRoom, adminFeedback);
                ns.CreateNotification(
                    student.StudentID,
                    "Student",
                    "Room Change Request Approved",
                    "Your room change request has been approved. You have been removed from your current room. " +
                    "The building manager will allocate you to a new room shortly.",
                    "RoomChangeApproved",
                    request.RequestID,
                    "RoomChangeRequest"
                );
            }
            else
            {
                // Declined — student stays in their current room, no changes
                var ns = new NotificationService();
                ns.SendRoomChangeDeclinedEmail(student, currentRoom, adminFeedback);
                ns.CreateNotification(
                    student.StudentID,
                    "Student",
                    "Room Change Request Declined",
                    $"Your room change request has been declined. {(!string.IsNullOrEmpty(adminFeedback) ? "Reason: " + adminFeedback : "")}",
                    "RoomChangeDeclined",
                    request.RequestID,
                    "RoomChangeRequest"
                );
            }

            request.Status = decision;
            request.AdminFeedback = adminFeedback;
            request.DateReviewed = DateTime.Now;
            request.ReviewedByStaffID = staff.StaffID;

            db.SaveChanges();

            TempData["SuccessMessage"] = decision == "Approved"
                ? $"Request #{requestId} approved. Student has been removed from their room and will appear in the unallocated list."
                : $"Request #{requestId} declined. Student remains in their current room.";

            return RedirectToAction("RoomChangeRequests");
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────────────

       
        private string GenerateQRToken(int studentId, int residenceId)
        {
            string secret = System.Configuration.ConfigurationManager.AppSettings["QRSecretKey"];
            if (String.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("QRSecretKey must be configured in the deployment environment.");

            string raw = $"{studentId}:{residenceId}:{secret}";

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 32).ToLower();
            }
        }

        // ── CHECK-IN MANAGEMENT ───────────────────────────────────────────────────────

        // GET: Staff/CheckInManagement
        // Building manager sees all students in their residence + QR status
        public ActionResult CheckInManagement()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null || staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can manage check-ins.";
                return RedirectToAction("Dashboard", "Staff");
            }

            if (staff.ResidenceID == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to a residence yet.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var residence = db.Residences.Find(staff.ResidenceID);

            // All students allocated to this residence
            var students = db.Students
                .Include(s => s.Room)
                .Where(s => s.ResidenceID == staff.ResidenceID)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToList();

            // All check-in records for this residence
            var checkIns = db.ResidenceCheckIns
                .Where(c => c.ResidenceID == staff.ResidenceID)
                .ToList();

            // Stats for the dashboard cards
            ViewBag.Residence = residence;
            ViewBag.CheckIns = checkIns;
            ViewBag.TotalStudents = students.Count;
            ViewBag.CheckedInCount = checkIns.Count(c => c.HasCheckedIn);
            ViewBag.NotCheckedIn = students.Count - checkIns.Count(c => c.HasCheckedIn);
            ViewBag.CheckedOutCount = checkIns.Count(c => c.HasCheckedOut);
            ViewBag.QRGeneratedCount = checkIns.Count;
            ViewBag.BaseUrl = Request.Url.GetLeftPart(UriPartial.Authority);

            return View(students);
        }

        // POST: Staff/GenerateStudentQR
        // Generates (or regenerates) QR token for ONE student
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateStudentQR(int studentId)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            var student = db.Students.Find(studentId);

            if (staff == null || staff.Role != "Building Manager" || staff.ResidenceID == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("CheckInManagement");
            }

            if (student == null || student.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "Student not found in your residence.";
                return RedirectToAction("CheckInManagement");
            }

            string token = GenerateQRToken(studentId, staff.ResidenceID.Value);
            var existing = db.ResidenceCheckIns
                .FirstOrDefault(c => c.StudentID == studentId &&
                                     c.ResidenceID == staff.ResidenceID.Value);

            if (existing != null)
            {
                // Regenerating resets the check-in so the student must scan fresh
                existing.QRToken = token;
                existing.TokenGeneratedAt = DateTime.Now;
                existing.GeneratedByStaffID = staff.StaffID;
                existing.HasCheckedIn = false;
                existing.CheckInTime = null;
                existing.HasCheckedOut = false;
                existing.CheckOutTime = null;
            }
            else
            {
                db.ResidenceCheckIns.Add(new ResidenceCheckIn
                {
                    StudentID = studentId,
                    ResidenceID = staff.ResidenceID.Value,
                    QRToken = token,
                    TokenGeneratedAt = DateTime.Now,
                    GeneratedByStaffID = staff.StaffID,
                    HasCheckedIn = false,
                    HasCheckedOut = false
                });
            }

            db.SaveChanges();

            TempData["SuccessMessage"] =
                $"QR code generated for {student.FirstName} {student.LastName}. " +
                "You can now view and share their QR code.";

            return RedirectToAction("CheckInManagement");
        }

        // POST: Staff/GenerateAllQRCodes
        // Generates QR codes for ALL students in the residence at once
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateAllQRCodes()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null || staff.Role != "Building Manager" || staff.ResidenceID == null)
            {
                TempData["ErrorMessage"] = "Unauthorized.";
                return RedirectToAction("CheckInManagement");
            }

            var students = db.Students
                .Where(s => s.ResidenceID == staff.ResidenceID)
                .ToList();

            if (!students.Any())
            {
                TempData["ErrorMessage"] = "No students are allocated to your residence yet.";
                return RedirectToAction("CheckInManagement");
            }

            int generated = 0;
            foreach (var student in students)
            {
                string token = GenerateQRToken(student.StudentID, staff.ResidenceID.Value);
                var existing = db.ResidenceCheckIns
                    .FirstOrDefault(c => c.StudentID == student.StudentID &&
                                         c.ResidenceID == staff.ResidenceID.Value);

                if (existing != null)
                {
                    existing.QRToken = token;
                    existing.TokenGeneratedAt = DateTime.Now;
                    existing.GeneratedByStaffID = staff.StaffID;
                    existing.HasCheckedIn = false;
                    existing.CheckInTime = null;
                    existing.HasCheckedOut = false;
                    existing.CheckOutTime = null;
                }
                else
                {
                    db.ResidenceCheckIns.Add(new ResidenceCheckIn
                    {
                        StudentID = student.StudentID,
                        ResidenceID = staff.ResidenceID.Value,
                        QRToken = token,
                        TokenGeneratedAt = DateTime.Now,
                        GeneratedByStaffID = staff.StaffID,
                        HasCheckedIn = false,
                        HasCheckedOut = false
                    });
                }
                generated++;
            }

            db.SaveChanges();

            TempData["SuccessMessage"] =
                $"QR codes generated for all {generated} student(s) in your residence. " +
                "Click 'View QR' next to each student to see their individual QR code.";

            return RedirectToAction("CheckInManagement");
        }

        // GET: Staff/ViewStudentQR/{checkInId}
        // Shows the actual QR code page for a specific student (manager prints/shares this)
        // GET: Staff/ViewStudentQR/{checkInId}
        // Shows the actual QR code page for a specific student (manager prints/shares this)
        public ActionResult ViewStudentQR(int checkInId)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            var checkIn = db.ResidenceCheckIns
                .Include(c => c.Student)
                .Include(c => c.Residence)
                .FirstOrDefault(c => c.CheckInID == checkInId);

            if (checkIn == null || staff == null || checkIn.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "QR code not found.";
                return RedirectToAction("CheckInManagement");
            }

            string baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            string checkInUrl = $"{baseUrl}/Student/ScanCheckIn?token={checkIn.QRToken}";
            string checkOutUrl = $"{baseUrl}/Student/ScanCheckOut?token={checkIn.QRToken}";

            // REPLACED THIS PART - Now using QuickChart.io
            string checkInQRImageUrl = $"https://quickchart.io/qr?text={Uri.EscapeDataString(checkInUrl)}&size=300";
            string checkOutQRImageUrl = $"https://quickchart.io/qr?text={Uri.EscapeDataString(checkOutUrl)}&size=300";

            ViewBag.CheckIn = checkIn;
            ViewBag.CheckInUrl = checkInUrl;
            ViewBag.CheckOutUrl = checkOutUrl;
            ViewBag.CheckInQRImage = checkInQRImageUrl;
            ViewBag.CheckOutQRImage = checkOutQRImageUrl;

            return View();
        }

        // GET: Staff/CheckInLog
        // Full log of check-ins and check-outs for the building manager's residence
        public ActionResult CheckInLog()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);

            if (staff == null || staff.Role != "Building Manager")
            {
                TempData["ErrorMessage"] = "Only Building Managers can view the check-in log.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var log = db.ResidenceCheckIns
                .Include(c => c.Student)
                .Include(c => c.Residence)
                .Where(c => c.ResidenceID == staff.ResidenceID)
                .OrderByDescending(c => c.CheckInTime)
                .ToList();

            ViewBag.ResidenceName = db.Residences.Find(staff.ResidenceID)?.Name;
            ViewBag.CheckedInCount = log.Count(c => c.HasCheckedIn);
            ViewBag.CheckedOutCount = log.Count(c => c.HasCheckedOut);
            ViewBag.PendingCount = log.Count(c => !c.HasCheckedIn);

            return View(log);
        }
        // ── GET: Staff/ResidenceChangeRequests ────────────────────────────────────────
        // Admin sees ALL residence change requests across all residences
        public ActionResult ResidenceChangeRequests()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                TempData["ErrorMessage"] =
                    "Only Admins can review residence change requests.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var requests = db.ResidenceChangeRequests
                .Include(r => r.Student)
                .Include(r => r.CurrentResidence)
                .Include(r => r.ReviewedBy)
                .OrderByDescending(r => r.DateRequested)
                .ToList();

            // Load each student's current room manually
            foreach (var r in requests)
            {
                if (r.Student != null && r.Student.RoomID.HasValue)
                    r.Student.Room = db.Rooms.Find(r.Student.RoomID.Value);
            }

            ViewBag.PendingCount = requests.Count(r => r.Status == "Pending");
            ViewBag.ApprovedCount = requests.Count(r => r.Status == "Approved");
            ViewBag.DeclinedCount = requests.Count(r => r.Status == "Declined");

            return View(requests);
        }

        // ── POST: Staff/ReviewResidenceChangeRequest ───────────────────────────────────
        // Admin approves or declines
        // APPROVE → student deallocated from current residence AND room
        // DECLINE → student stays, nothing changes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewResidenceChangeRequest(
    int requestId, string decision, string adminFeedback)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null || staff.Role != "Admin")
            {
                TempData["ErrorMessage"] = "Only Admins can review residence change requests.";
                return RedirectToAction("Dashboard", "Staff");
            }

            var request = db.ResidenceChangeRequests
                .Include(r => r.CurrentResidence)
                .FirstOrDefault(r => r.RequestID == requestId);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction("ResidenceChangeRequests");
            }

            if (request.Status != "Pending")
            {
                TempData["ErrorMessage"] = "This request has already been reviewed.";
                return RedirectToAction("ResidenceChangeRequests");
            }

            // Load student with residence
            var student = db.Students
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StudentID == request.StudentID);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student not found.";
                return RedirectToAction("ResidenceChangeRequests");
            }

            // Get current residence (the one they want to leave)
            var currentResidence = request.CurrentResidence ??
                                   db.Residences.Find(request.CurrentResidenceID);

            // Update the request
            request.Status = decision;   // "Approved" or "Declined"
            request.AdminFeedback = adminFeedback;
            request.DateReviewed = DateTime.Now;
            request.ReviewedByStaffID = staff.StaffID;

            string residenceName = currentResidence?.Name ?? "their residence";
            string roomNumber = null;

            if (decision == "Approved")
            {
                // ── DEALLOCATE STUDENT FROM CURRENT RESIDENCE AND ROOM ──
                // 1. If student has a room, free that room
                if (student.RoomID != null)
                {
                    var room = db.Rooms.Find(student.RoomID);
                    if (room != null)
                    {
                        roomNumber = room.RoomNumber;
                        room.Status = "Available";
                    }
                    student.RoomID = null;
                }

                // 2. Remove residence allocation
                student.ResidenceID = null;

                // 3. Reset check-in record
                var checkIn = db.ResidenceCheckIns
                    .FirstOrDefault(c => c.StudentID == student.StudentID &&
                                         c.ResidenceID == request.CurrentResidenceID);
                if (checkIn != null)
                {
                    checkIn.HasCheckedIn = false;
                    checkIn.CheckInTime = null;
                    checkIn.HasCheckedOut = false;
                    checkIn.CheckOutTime = null;
                }
            }

            db.SaveChanges();

            // ── SEND EMAIL USING THE DEDICATED METHODS ──
            try
            {
                var ns = new NotificationService();

                if (decision == "Approved")
                {
                    // Use the dedicated approval email method
                    ns.SendResidenceChangeApprovalEmail(student, currentResidence, adminFeedback);
                }
                else // Declined
                {
                    // Use the dedicated decline email method
                    ns.SendResidenceChangeDeclinedEmail(student, adminFeedback);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't break the user flow
                System.Diagnostics.Debug.WriteLine($"Email failed: {ex.Message}");
                TempData["EmailWarning"] = "Request processed but email notification failed.";
            }

            // Build success message for admin
            string msg = decision == "Approved"
                ? $"Request approved. {student.FirstName} {student.LastName} has been " +
                  $"deallocated from {residenceName}" +
                  (roomNumber != null ? $" and Room {roomNumber}" : "") +
                  ". You can now allocate them to a new residence from Student Allocation."
                : $"Request declined. {student.FirstName} {student.LastName} " +
                  $"remains in {residenceName}.";

            TempData["SuccessMessage"] = msg;
            return RedirectToAction("ResidenceChangeRequests");
        }
        // ─────────────────────────────────────────────────────────────────────────────
        // GET: Staff/Reports
        // Building managers see only their residence's requests.
        // Admins see all requests across every residence.
        // ─────────────────────────────────────────────────────────────────────────────
        public ActionResult Reports()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            IQueryable<Maintenance> query = db.Maintenances
                .Include(m => m.Student)
                .Include(m => m.Student.Residence)
                .Include(m => m.Technician);

            if (staff.Role == "Building Manager")
                query = query.Where(m => m.Student.ResidenceID == staff.ResidenceID);
            // Admins see everything — no filter applied

            var requests = query
                .OrderByDescending(m => m.DateReported)
                .ToList();

            ViewBag.StaffRole = staff.Role;
            ViewBag.ResidenceName = staff.Residence?.Name;

            return View(requests);
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // POST: Staff/ResolveMaintenance
        // Marks a single maintenance request as Resolved and notifies the student.
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResolveMaintenance(int id)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            var maintenance = db.Maintenances
                .Include(m => m.Student)
                .FirstOrDefault(m => m.MaintenanceID == id);

            if (maintenance == null)
            {
                TempData["ErrorMessage"] = "Maintenance request not found.";
                return RedirectToAction("Reports");
            }

            // Building managers may only resolve requests for their own residence
            if (staff.Role == "Building Manager" &&
                maintenance.Student?.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only manage requests for your residence.";
                return RedirectToAction("Reports");
            }

            maintenance.Status = "Resolved";
            maintenance.DateResolved = DateTime.Now;
            maintenance.StaffID = staff.StaffID;

            db.SaveChanges();

            try
            {
                var notificationService = new NotificationService();
                notificationService.NotifyStudentMaintenanceResolved(maintenance.MaintenanceID);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification failed: {ex.Message}");
                TempData["EmailWarning"] = "Request resolved but the student notification email failed.";
            }

            TempData["SuccessMessage"] = $"Maintenance request #{id} has been marked as resolved.";
            return RedirectToAction("Reports");
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // POST: Staff/UpdateMaintenanceStatus
        // Updates a request to Pending, In Progress, or Resolved.
        // Sends a student notification when the status is set to Resolved.
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateMaintenanceStatus(int id, string status)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            var maintenance = db.Maintenances
                .Include(m => m.Student)
                .FirstOrDefault(m => m.MaintenanceID == id);

            if (maintenance == null)
            {
                TempData["ErrorMessage"] = "Maintenance request not found.";
                return RedirectToAction("Reports");
            }

            // Building managers may only update requests for their own residence
            if (staff.Role == "Building Manager" &&
                maintenance.Student?.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only manage requests for your residence.";
                return RedirectToAction("Reports");
            }

            // Reject any status value that is not in the allowed list
            var allowedStatuses = new[] { "Pending", "In Progress", "Resolved" };
            if (!allowedStatuses.Contains(status))
            {
                TempData["ErrorMessage"] = "Invalid status value.";
                return RedirectToAction("Reports");
            }

            maintenance.Status = status;
            maintenance.StaffID = staff.StaffID;

            if (status == "Resolved")
            {
                maintenance.DateResolved = DateTime.Now;

                try
                {
                    var notificationService = new NotificationService();
                    notificationService.NotifyStudentMaintenanceResolved(maintenance.MaintenanceID);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Notification failed: {ex.Message}");
                    TempData["EmailWarning"] = "Status updated but the student notification email failed.";
                }
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Request #{id} has been updated to '{status}'.";
            return RedirectToAction("Reports");
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // GET: Staff/GetTechnicians?profession=Plumber
        // Returns a JSON list of technicians filtered by profession,
        // including each technician's current unresolved job count.
        // ─────────────────────────────────────────────────────────────────────────────
        public JsonResult GetTechnicians(string profession)
        {
            if (Session["StaffID"] == null)
                return Json(new { error = "Unauthorised" }, JsonRequestBehavior.AllowGet);

            if (string.IsNullOrWhiteSpace(profession))
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            var technicians = db.Technicians
                .Where(t => t.TechnicianType == profession)
                .Select(t => new
                {
                    t.TechnicianID,
                    t.FullName,
                    PendingJobs = t.Maintenances.Count(m => m.Status != "Resolved")
                })
                .OrderBy(t => t.PendingJobs)   // Show least-busy technicians first
                .ToList();

            return Json(technicians, JsonRequestBehavior.AllowGet);
        }


        // ─────────────────────────────────────────────────────────────────────────────
        // POST: Staff/AssignTechnician
        // Assigns an available technician to a pending maintenance request
        // and updates its status to In Progress.
        // ─────────────────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignTechnician(int maintenanceId, int technicianId)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var technician = db.Technicians.Find(technicianId);
            if (technician == null)
            {
                TempData["ErrorMessage"] = "Technician not found.";
                return RedirectToAction("Reports");
            }

            var maintenance = db.Maintenances.Find(maintenanceId);
            if (maintenance == null)
            {
                TempData["ErrorMessage"] = "Maintenance request not found.";
                return RedirectToAction("Reports");
            }

            // Enforce the 5-job cap to prevent overloading a technician
            int activeJobs = db.Maintenances
                .Count(m => m.TechnicianID == technicianId && m.Status != "Resolved");

            if (activeJobs >= 5)
            {
                TempData["ErrorMessage"] =
                    $"{technician.FullName} is currently unavailable — they already have {activeJobs} unresolved jobs.";
                return RedirectToAction("Reports");
            }

            maintenance.TechnicianID = technicianId;
            maintenance.Status = "In Progress";

            db.SaveChanges();

            TempData["SuccessMessage"] =
                $"{technician.FullName} has been successfully assigned to request #{maintenanceId}.";
            return RedirectToAction("Reports");
        }

        // GET: Staff/ComplaintManagement
        public ActionResult ComplaintManagement()
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staffId = (int)Session["StaffID"];
            var staff = db.Staffs
                .Include(s => s.Residence)
                .FirstOrDefault(s => s.StaffID == staffId);

            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            // Get data first, then transform in memory
            // This avoids EF trying to translate complex expressions

            // First, get the complaints with their students
            var complaintsQuery = db.Complaints
                .Where(c => c.StudentID != null);

            // Filter by residence for Building Managers
            if (staff.Role == "Building Manager" && staff.ResidenceID.HasValue)
            {
                // Get student IDs that belong to this residence
                var studentIdsInResidence = db.Students
                    .Where(s => s.ResidenceID == staff.ResidenceID.Value)
                    .Select(s => s.StudentID)
                    .ToList();

                complaintsQuery = complaintsQuery.Where(c => studentIdsInResidence.Contains(c.StudentID.Value) || (c.ReportedStudentID.HasValue && studentIdsInResidence.Contains(c.ReportedStudentID.Value)));
            }

            // Execute the query and get the complaints
            var complaints = complaintsQuery
                .OrderByDescending(c => c.DateSubmitted)
                .ToList();

            // Now transform to ViewModel in memory (LINQ to Objects, not Entities)
            var complaintViewModels = new List<ComplaintManagementViewModel>();

            foreach (var complaint in complaints)
            {
                var student = db.Students
                    .Include(s => s.Residence)
                    .FirstOrDefault(s => s.StudentID == complaint.StudentID);
                var reportedStudent = complaint.ReportedStudentID.HasValue ? db.Students.FirstOrDefault(s => s.StudentID == complaint.ReportedStudentID.Value) : null;

                if (student != null)
                {
                    var viewModel = new ComplaintManagementViewModel
                    {
                        ComplaintId = complaint.ComplaintId,
                        StudentID = complaint.StudentID,
                        ReportedStudentID = complaint.ReportedStudentID,
                        Subject = complaint.Subject,
                        Category = complaint.Category,
                        Description = complaint.Description,
                        Status = complaint.Status,
                        ManagerFeedback = complaint.ManagerFeedback,
                        DateSubmitted = complaint.DateSubmitted,
                        LastUpdated = complaint.LastUpdated,
                        DateResolved = complaint.DateResolved,
                        StudentName = (student.FirstName ?? "") + " " + (student.LastName ?? ""),
                        StudentNumber = student.StudentNumber,
                        ReportedStudentName = reportedStudent == null ? "Not specified" : (reportedStudent.FirstName + " " + reportedStudent.LastName),
                        ReportedStudentNumber = reportedStudent == null ? null : reportedStudent.StudentNumber,
                        ResidenceName = student.Residence?.Name,
                        ResidenceID = student.ResidenceID,
                        WarningIssued = complaint.WarningIssued,
                        WarningSeverity = complaint.WarningSeverity,
                        WarningReason = complaint.WarningReason
                    };

                    complaintViewModels.Add(viewModel);
                }
            }

            ViewBag.StaffRole = staff.Role;
            ViewBag.ResidenceName = staff.Residence?.Name;

            return View(complaintViewModels);
        }

        // POST: Staff/UpdateComplaintStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateComplaintStatus(int id, string status, string managerFeedback, bool issueWarning = false, string warningSeverity = null, string warningReason = null)
        {
            if (Session["StaffID"] == null)
                return RedirectToAction("StaffLogin", "Auth");

            var staff = db.Staffs.Find((int)Session["StaffID"]);
            if (staff == null)
                return RedirectToAction("StaffLogin", "Auth");

            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == id);
            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction("ComplaintManagement");
            }

            // Authorisation: Building Managers may only update their own residence's complaints
            Student complaintStudent = null;
            if (complaint.StudentID.HasValue)
            {
                complaintStudent = db.Students.Find(complaint.StudentID.Value);
            }

            var reportedStudent = complaint.ReportedStudentID.HasValue ? db.Students.Find(complaint.ReportedStudentID.Value) : null;
            if (staff.Role == "Building Manager" &&
                complaintStudent?.ResidenceID != staff.ResidenceID && reportedStudent?.ResidenceID != staff.ResidenceID)
            {
                TempData["ErrorMessage"] = "You can only manage complaints for your residence.";
                return RedirectToAction("ComplaintManagement");
            }

            // Validate status value
            var allowedStatuses = new[] { "Pending", "In Progress", "Resolved" };
            if (!allowedStatuses.Contains(status))
            {
                TempData["ErrorMessage"] = "Invalid complaint status.";
                return RedirectToAction("ComplaintManagement");
            }

            // Apply changes
            complaint.Status = status;
            complaint.ManagerFeedback = string.IsNullOrWhiteSpace(managerFeedback)
                                              ? null
                                              : managerFeedback.Trim();
            complaint.LastUpdated = DateTime.Now;
            complaint.ReviewedByStaffID = staff.StaffID;
            complaint.DateResolved = status == "Resolved" ? DateTime.Now : (DateTime?)null;

            if (issueWarning)
            {
                var permittedSeverities = new[] { "Minor", "Serious", "Critical" };
                if (reportedStudent == null || !permittedSeverities.Contains(warningSeverity) || string.IsNullOrWhiteSpace(warningReason))
                {
                    TempData["ErrorMessage"] = "A warning needs a reported student, valid severity, and reason.";
                    return RedirectToAction("ComplaintManagement");
                }
                if (!complaint.WarningIssued)
                {
                    complaint.WarningIssued = true;
                    complaint.WarningSeverity = warningSeverity;
                    complaint.WarningReason = warningReason.Trim();
                    complaint.WarningIssuedAt = DateTime.Now;
                    complaint.WarningIssuedByStaffID = staff.StaffID;
                    db.StudentConductRecords.Add(new StudentConductRecord { StudentID = reportedStudent.StudentID, ComplaintId = complaint.ComplaintId, Severity = warningSeverity, Reason = warningReason.Trim(), IssuedByStaffID = staff.StaffID });
                    new NotificationService().CreateNotification(reportedStudent.StudentID, "Student", "Conduct warning issued", "A " + warningSeverity + " conduct warning has been recorded on your residence history. Please contact residence administration for details.", "ConductWarning", complaint.ComplaintId, "Complaint");
                }
            }

            db.SaveChanges();

            // FIXED: Pass all three required parameters
            var notificationService = new NotificationService();
            notificationService.NotifyStudentComplaintUpdated(
                complaint.ComplaintId,  // complaintId
                status,                 // status
                managerFeedback         // response
            );

            TempData["SuccessMessage"] = $"Complaint #{complaint.ComplaintId} has been updated to '{status}'.";
            return RedirectToAction("ComplaintManagement");
        }

        private Staff GetLoggedInStaff()
        {
            if (Session["StaffID"] == null)
            {
                return null;
            }

            int staffId = (int)Session["StaffID"];
            return db.Staffs.Include(s => s.Residence).FirstOrDefault(s => s.StaffID == staffId);
        }

        private bool CanAccessResidenceOperations(Staff staff)
        {
            return staff != null && (staff.Role == "Admin" || staff.Role == "Building Manager" || staff.Role == "Security");
        }

        private IQueryable<Student> StudentsForStaffResidence(Staff staff)
        {
            var students = db.Students.Include(s => s.Room).Include(s => s.Residence).AsQueryable();
            if (staff.Role != "Admin" && staff.ResidenceID.HasValue)
            {
                students = students.Where(s => s.ResidenceID == staff.ResidenceID.Value);
            }

            return students;
        }

        private IQueryable<Maintenance> MaintenanceForStaffResidence(Staff staff)
        {
            var query = db.Maintenances.Include(m => m.Student).Include(m => m.Room).Include(m => m.Technician).AsQueryable();
            if (staff.Role != "Admin" && staff.ResidenceID.HasValue)
            {
                query = query.Where(m => m.Student.ResidenceID == staff.ResidenceID.Value);
            }

            return query;
        }

        private IQueryable<Complaint> ComplaintsForStaffResidence(Staff staff)
        {
            var query = db.Complaints.Include(c => c.Student).AsQueryable();
            if (staff.Role != "Admin" && staff.ResidenceID.HasValue)
            {
                query = query.Where(c => c.StudentID.HasValue && c.Student.ResidenceID == staff.ResidenceID.Value);
            }

            return query;
        }

        private IQueryable<Visitor> VisitorsForStaffResidence(Staff staff)
        {
            var query = db.Visitors.AsQueryable();
            if (staff.Role != "Admin" && staff.ResidenceID.HasValue)
            {
                query = query.Where(v => v.ResidenceID == staff.ResidenceID.Value);
            }

            return query;
        }

        private DateTime CalculateMaintenanceTarget(Maintenance maintenance)
        {
            if (maintenance.IssueType == MaintenanceIssueType.Security || maintenance.IsSafetyCritical)
            {
                return maintenance.DateReported.AddHours(4);
            }

            if (maintenance.IssueType == MaintenanceIssueType.Electrical || maintenance.IssueType == MaintenanceIssueType.Plumbing)
            {
                return maintenance.DateReported.AddHours(24);
            }

            return maintenance.DateReported.AddDays(3);
        }

        private string CalculateMaintenancePriority(Maintenance maintenance)
        {
            if (maintenance.IssueType == MaintenanceIssueType.Security || maintenance.IsSafetyCritical)
            {
                return "Critical";
            }

            if (maintenance.IssueType == MaintenanceIssueType.Electrical || maintenance.IssueType == MaintenanceIssueType.Plumbing)
            {
                return "High";
            }

            return "Normal";
        }

        private void ApplyMaintenanceSlaRules(Staff staff)
        {
            DateTime now = DateTime.Now;
            var openItems = MaintenanceForStaffResidence(staff)
                .Where(m => m.Status != "Resolved")
                .ToList();

            foreach (var item in openItems)
            {
                item.Priority = CalculateMaintenancePriority(item);
                item.TargetResponseBy = item.TargetResponseBy ?? CalculateMaintenanceTarget(item);

                if (item.TargetResponseBy.HasValue && item.TargetResponseBy.Value < now && !item.EscalatedAt.HasValue)
                {
                    item.EscalatedAt = now;
                    item.EscalationReason = "Maintenance request exceeded SLA response target.";
                }
            }
        }

        private void ApplyComplaintEscalationRules(Staff staff)
        {
            DateTime now = DateTime.Now;
            var openComplaints = ComplaintsForStaffResidence(staff)
                .Where(c => c.Status != "Resolved")
                .ToList();

            foreach (var complaint in openComplaints)
            {
                bool urgent = (complaint.Category ?? string.Empty).IndexOf("safety", StringComparison.OrdinalIgnoreCase) >= 0
                    || (complaint.Category ?? string.Empty).IndexOf("security", StringComparison.OrdinalIgnoreCase) >= 0
                    || (complaint.Description ?? string.Empty).IndexOf("threat", StringComparison.OrdinalIgnoreCase) >= 0;

                complaint.Priority = urgent ? "High" : (complaint.Priority ?? "Normal");
                complaint.TargetResolutionBy = complaint.TargetResolutionBy ?? complaint.DateSubmitted.AddDays(urgent ? 1 : 3);

                if (complaint.TargetResolutionBy.HasValue && complaint.TargetResolutionBy.Value < now && !complaint.EscalatedAt.HasValue)
                {
                    complaint.EscalatedAt = now;
                    complaint.EscalationReason = "Complaint exceeded target resolution time.";
                }
            }
        }

        private List<OccupancyForecastItem> BuildOccupancyForecast(Staff staff)
        {
            var residences = db.Residences.Include(r => r.Students).AsQueryable();
            if (staff.Role != "Admin" && staff.ResidenceID.HasValue)
            {
                residences = residences.Where(r => r.ResidenceID == staff.ResidenceID.Value);
            }

            var pendingApplications = db.ResidenceApplications
                .Where(a => a.Status == "Pending" || a.Status == "Submitted" || a.Status == null)
                .ToList();

            return residences
                .ToList()
                .Select(r =>
                {
                    int occupied = db.Students.Count(s => s.ResidenceID == r.ResidenceID);
                    int pending = pendingApplications.Count(a =>
                        string.IsNullOrWhiteSpace(r.Faculty)
                        || string.Equals(a.Faculty, r.Faculty, StringComparison.OrdinalIgnoreCase));
                    int available = Math.Max(0, r.Capacity - occupied);
                    decimal rate = r.Capacity == 0 ? 0 : Math.Round((decimal)occupied / r.Capacity * 100, 1);
                    string risk = rate >= 95 || pending > available ? "High" : rate >= 80 ? "Medium" : "Low";
                    string recommendation = risk == "High"
                        ? "Review waiting applications and consider reallocating demand to residences with available rooms."
                        : risk == "Medium"
                            ? "Monitor applications and prepare alternative room options."
                            : "Capacity is currently stable.";

                    return new OccupancyForecastItem
                    {
                        ResidenceID = r.ResidenceID,
                        ResidenceName = r.Name,
                        Capacity = r.Capacity,
                        Occupied = occupied,
                        PendingApplications = pending,
                        AvailableSpaces = available,
                        OccupancyRate = rate,
                        RiskLevel = risk,
                        Recommendation = recommendation
                    };
                })
                .OrderByDescending(f => f.OccupancyRate)
                .ToList();
        }

        public ActionResult Semester2Operations()
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            ApplyMaintenanceSlaRules(staff);
            ApplyComplaintEscalationRules(staff);
            db.SaveChanges();

            DateTime overstayThreshold = DateTime.Now.AddHours(-8);
            var model = new Semester2OperationsViewModel
            {
                Staff = staff,
                ResidenceName = staff.Residence != null ? staff.Residence.Name : "All Residences",
                EscalatedMaintenance = MaintenanceForStaffResidence(staff)
                    .Where(m => m.EscalatedAt.HasValue && m.Status != "Resolved")
                    .OrderByDescending(m => m.EscalatedAt)
                    .Take(20)
                    .ToList(),
                EscalatedComplaints = ComplaintsForStaffResidence(staff)
                    .Where(c => c.EscalatedAt.HasValue && c.Status != "Resolved")
                    .OrderByDescending(c => c.EscalatedAt)
                    .Take(20)
                    .ToList(),
                VisitorExceptions = VisitorsForStaffResidence(staff)
                    .Where(v => !v.CheckOutTime.HasValue && v.EntryTime.HasValue && v.EntryTime.Value < overstayThreshold)
                    .OrderBy(v => v.EntryTime)
                    .Take(20)
                    .ToList(),
                RecentInspections = db.RoomInspections
                    .Include(i => i.Room)
                    .Include(i => i.Student)
                    .Where(i => staff.Role == "Admin" || i.Room.ResidenceID == staff.ResidenceID)
                    .OrderByDescending(i => i.InspectionDate)
                    .Take(10)
                    .ToList(),
                OpenRollCalls = db.EmergencyRollCalls
                    .Include(r => r.Residence)
                    .Where(r => r.Status == "Open" && (staff.Role == "Admin" || r.ResidenceID == staff.ResidenceID))
                    .OrderByDescending(r => r.StartedAt)
                    .ToList(),
                OccupancyForecast = BuildOccupancyForecast(staff)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunMaintenanceEscalation()
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            ApplyMaintenanceSlaRules(staff);
            db.SaveChanges();
            TempData["SuccessMessage"] = "Maintenance SLA rules have been applied.";
            return RedirectToAction("Semester2Operations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunComplaintEscalation()
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            ApplyComplaintEscalationRules(staff);
            db.SaveChanges();
            TempData["SuccessMessage"] = "Complaint escalation rules have been applied.";
            return RedirectToAction("Semester2Operations");
        }

        public ActionResult RoomInspection(int? roomId)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var rooms = db.Rooms
                .Where(r => staff.Role == "Admin" || r.ResidenceID == staff.ResidenceID)
                .OrderBy(r => r.RoomNumber)
                .ToList();

            ViewBag.Rooms = new SelectList(rooms, "RoomID", "RoomNumber", roomId);
            ViewBag.Students = new SelectList(StudentsForStaffResidence(staff).OrderBy(s => s.FirstName).ToList(), "StudentID", "StudentNumber");
            ViewBag.RecentInspections = db.RoomInspections
                .Include(i => i.Room)
                .Include(i => i.Student)
                .Where(i => staff.Role == "Admin" || i.Room.ResidenceID == staff.ResidenceID)
                .OrderByDescending(i => i.InspectionDate)
                .Take(20)
                .ToList();

            return View(new RoomInspection { RoomID = roomId ?? 0, InspectionType = "Move-In", ConditionStatus = "Good" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RoomInspection(RoomInspection model)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var room = db.Rooms.Find(model.RoomID);
            if (room == null || (staff.Role != "Admin" && room.ResidenceID != staff.ResidenceID))
            {
                TempData["ErrorMessage"] = "Room not found in your residence.";
                return RedirectToAction("RoomInspection");
            }

            model.InspectionDate = DateTime.Now;
            model.InspectedByStaffID = staff.StaffID;
            db.RoomInspections.Add(model);

            if (model.BlocksAllocation)
            {
                room.Status = "Unavailable";
            }

            if (model.RequiresMaintenance)
            {
                var affectedStudent = model.StudentID.HasValue ? db.Students.Find(model.StudentID.Value) : null;
                int? affectedStudentId = affectedStudent != null
                    ? affectedStudent.StudentID
                    : db.Students.Where(s => s.RoomID == model.RoomID).Select(s => (int?)s.StudentID).FirstOrDefault();

                if (affectedStudentId.HasValue)
                {
                    db.Maintenances.Add(new Maintenance
                    {
                        StudentID = affectedStudentId.Value,
                        RoomID = model.RoomID,
                        RoomNumber = room.RoomNumber,
                        IssueType = MaintenanceIssueType.Other,
                        IssueDescription = "Room inspection follow-up: " + (model.Notes ?? "Maintenance required after inspection."),
                        DateReported = DateTime.Now,
                        Status = "Pending",
                        Priority = model.BlocksAllocation ? "High" : "Normal",
                        TargetResponseBy = DateTime.Now.AddDays(model.BlocksAllocation ? 1 : 3),
                        StaffID = staff.StaffID
                    });
                }
            }

            db.SaveChanges();
            TempData["SuccessMessage"] = "Room inspection recorded successfully.";
            return RedirectToAction("RoomInspection");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StartEmergencyRollCall(string incidentTitle, string incidentNotes)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff) || !staff.ResidenceID.HasValue)
            {
                return RedirectToAction("Semester2Operations");
            }

            if (string.IsNullOrWhiteSpace(incidentTitle))
            {
                incidentTitle = "Emergency Roll Call";
            }

            var rollCall = new EmergencyRollCall
            {
                ResidenceID = staff.ResidenceID.Value,
                IncidentTitle = incidentTitle.Trim(),
                IncidentNotes = incidentNotes,
                Status = "Open",
                StartedAt = DateTime.Now,
                StartedByStaffID = staff.StaffID
            };

            db.EmergencyRollCalls.Add(rollCall);
            db.SaveChanges();

            var students = StudentsForStaffResidence(staff).Where(s => s.IsActive).ToList();
            foreach (var student in students)
            {
                db.EmergencyRollCallPeople.Add(new EmergencyRollCallPerson
                {
                    EmergencyRollCallID = rollCall.EmergencyRollCallID,
                    PersonType = "Student",
                    StudentID = student.StudentID,
                    DisplayName = student.FirstName + " " + student.LastName,
                    RoomNumber = student.Room != null ? student.Room.RoomNumber : "No room",
                    SafetyStatus = "Unknown"
                });
            }

            var visitors = VisitorsForStaffResidence(staff)
                .Where(v => v.IsActive && v.EntryTime.HasValue && !v.CheckOutTime.HasValue)
                .ToList();

            foreach (var visitor in visitors)
            {
                db.EmergencyRollCallPeople.Add(new EmergencyRollCallPerson
                {
                    EmergencyRollCallID = rollCall.EmergencyRollCallID,
                    PersonType = "Visitor",
                    VisitorID = visitor.VisitorID,
                    DisplayName = visitor.FullName,
                    RoomNumber = "Visiting student #" + visitor.StudentID,
                    SafetyStatus = "Unknown"
                });
            }

            db.SaveChanges();
            return RedirectToAction("EmergencyRollCallDetails", new { id = rollCall.EmergencyRollCallID });
        }

        public ActionResult EmergencyRollCallDetails(int id)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var rollCall = db.EmergencyRollCalls.Include(r => r.Residence).FirstOrDefault(r => r.EmergencyRollCallID == id);
            if (rollCall == null || (staff.Role != "Admin" && rollCall.ResidenceID != staff.ResidenceID))
            {
                TempData["ErrorMessage"] = "Roll call not found.";
                return RedirectToAction("Semester2Operations");
            }

            var model = new EmergencyRollCallViewModel
            {
                RollCall = rollCall,
                People = db.EmergencyRollCallPeople
                    .Where(p => p.EmergencyRollCallID == id)
                    .OrderBy(p => p.PersonType)
                    .ThenBy(p => p.DisplayName)
                    .Select(p => new RollCallPersonViewModel
                    {
                        EmergencyRollCallPersonID = p.EmergencyRollCallPersonID,
                        PersonType = p.PersonType,
                        DisplayName = p.DisplayName,
                        RoomNumber = p.RoomNumber,
                        SafetyStatus = p.SafetyStatus,
                        Notes = p.Notes
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkRollCallPerson(int personId, string safetyStatus, string notes)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var person = db.EmergencyRollCallPeople.Include(p => p.EmergencyRollCall).FirstOrDefault(p => p.EmergencyRollCallPersonID == personId);
            if (person == null || (staff.Role != "Admin" && person.EmergencyRollCall.ResidenceID != staff.ResidenceID))
            {
                TempData["ErrorMessage"] = "Roll call person not found.";
                return RedirectToAction("Semester2Operations");
            }
            if (person.EmergencyRollCall.Status != "Open")
            {
                TempData["ErrorMessage"] = "Safety statuses are locked after the emergency roll call has concluded.";
                return RedirectToAction("EmergencyRollCallDetails", new { id = person.EmergencyRollCallID });
            }

            var allowedStatuses = new[] { "Unknown", "Safe", "Missing", "Outside" };
            person.SafetyStatus = allowedStatuses.Contains(safetyStatus) ? safetyStatus : "Unknown";
            person.Notes = notes;
            person.MarkedAt = DateTime.Now;
            person.MarkedByStaffID = staff.StaffID;
            db.SaveChanges();

            return RedirectToAction("EmergencyRollCallDetails", new { id = person.EmergencyRollCallID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CloseEmergencyRollCall(int id, string conclusionNotes, bool confirmConclusion)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            var rollCall = db.EmergencyRollCalls.FirstOrDefault(r => r.EmergencyRollCallID == id);
            if (rollCall == null || (staff.Role != "Admin" && rollCall.ResidenceID != staff.ResidenceID))
            {
                TempData["ErrorMessage"] = "Roll call not found.";
                return RedirectToAction("Semester2Operations");
            }
            if (rollCall.Status != "Open")
            {
                TempData["ErrorMessage"] = "This emergency roll call has already been concluded.";
                return RedirectToAction("EmergencyRollCallDetails", new { id });
            }
            if (!confirmConclusion)
            {
                TempData["ErrorMessage"] = "Confirm that you have reviewed the current safety statuses before concluding the roll call.";
                return RedirectToAction("EmergencyRollCallDetails", new { id });
            }

            var outstandingCount = db.EmergencyRollCallPeople.Count(p => p.EmergencyRollCallID == id && (p.SafetyStatus == "Unknown" || p.SafetyStatus == "Missing"));
            if (outstandingCount > 0 && string.IsNullOrWhiteSpace(conclusionNotes))
            {
                TempData["ErrorMessage"] = "Record the outstanding or unaccounted-for people in the conclusion notes before closing this roll call.";
                return RedirectToAction("EmergencyRollCallDetails", new { id });
            }

            rollCall.Status = "Concluded";
            rollCall.ClosedAt = DateTime.Now;
            rollCall.ConcludedByStaffID = staff.StaffID;
            rollCall.ConclusionNotes = string.IsNullOrWhiteSpace(conclusionNotes) ? null : conclusionNotes.Trim();
            rollCall.OutstandingPeopleCount = outstandingCount;
            db.SaveChanges();
            TempData["SuccessMessage"] = "Emergency roll call concluded and final results preserved.";
            return RedirectToAction("EmergencyRollCallDetails", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendTargetedResidenceAlert(string audience, string title, string message)
        {
            var staff = GetLoggedInStaff();
            if (!CanAccessResidenceOperations(staff))
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Alert title and message are required.";
                return RedirectToAction("Semester2Operations");
            }

            var notificationService = new NotificationService();
            int sent = 0;

            if (audience == "Students" || audience == "Everyone")
            {
                foreach (var student in StudentsForStaffResidence(staff).ToList())
                {
                    notificationService.CreateNotification(student.StudentID, "Student", title.Trim(), message.Trim(), "ResidenceAlert", null, "Semester2Operations");
                    sent++;
                }
            }

            if (audience == "Staff" || audience == "Everyone")
            {
                var staffQuery = db.Staffs.AsQueryable();
                if (staff.Role != "Admin" && staff.ResidenceID.HasValue)
                {
                    staffQuery = staffQuery.Where(s => s.ResidenceID == staff.ResidenceID.Value);
                }

                foreach (var staffMember in staffQuery.ToList())
                {
                    notificationService.CreateNotification(staffMember.StaffID, staffMember.Role == "Building Manager" ? "BuildingManager" : "Staff", title.Trim(), message.Trim(), "ResidenceAlert", null, "Semester2Operations");
                    sent++;
                }
            }

            TempData["SuccessMessage"] = $"Targeted alert sent to {sent} recipient(s).";
            return RedirectToAction("Semester2Operations");
        }

    }

}
