using DUTResManagementSystem.Models;
using DUTResManagementSystem.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DUTResManagementSystem.Controllers
{
    public class AuthController : Controller
    {
        private ResContext db = new ResContext();

        public ActionResult StudentLogin()
        {
            return View();
        }


        public ActionResult Dashboard()
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
            var maintenanceRequests = db.Maintenances
               .Where(m => m.StudentID == studentId)
               .OrderByDescending(m => m.DateReported)
               .Take(5)
               .ToList();
            var viewModel = new StudentDashboardViewModel
            {
                Student = student,
                MaintenanceRequests = maintenanceRequests
            };

            return View(viewModel);
        }

        // POST: Auth/StudentLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StudentLogin(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var student = db.Students.FirstOrDefault(s => s.Email == model.Email);
                if (student != null && student.PasswordHash == model.Password + "_hashed")
                {
                    Session["StudentID"] = student.StudentID;
                    Session["UserType"] = "Student";
                    if (Session["PendingCheckInToken"] != null)
                    {
                        string pendingToken = Session["PendingCheckInToken"].ToString();
                        Session.Remove("PendingCheckInToken");
                        return RedirectToAction("ScanCheckIn", "Student", new { token = pendingToken });
                    }

                    if (Session["PendingCheckOutToken"] != null)
                    {
                        string pendingToken = Session["PendingCheckOutToken"].ToString();
                        Session.Remove("PendingCheckOutToken");
                        return RedirectToAction("ScanCheckOut", "Student", new { token = pendingToken });
                    }
                    return RedirectToAction("Dashboard", "Student");
                }
                ModelState.AddModelError("", "Invalid email or password.");
            }
            return View(model);
        }

        // GET: Auth/StaffLogin
        public ActionResult StaffLogin()
        {
            return View();
        }

        // POST: Auth/StaffLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult StaffLogin(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var staff = db.Staffs.FirstOrDefault(s => s.Email == model.Email);

                // Simple password check
                if (staff != null && staff.Password == model.Password)
                {
                    Session["StaffID"] = staff.StaffID;
                    Session["UserType"] = staff.Role;
                    Session["StaffName"] = staff.FirstName + " " + staff.LastName;
                    Session["ResidenceID"] = staff.ResidenceID;

                    if (staff.Role == "Security")
                    {
                        return RedirectToAction("Dashboard", "Visitor");
                    }

                    return RedirectToAction("Dashboard", "Staff");
                }

                ModelState.AddModelError("", "Invalid email or password.");
            }

            return View(model);
        }

        // GET: Auth/Register
        public ActionResult Register()
        {
            // Get faculties for dropdown
            ViewBag.Faculties = new[] {
                "Accounting and Informatics",
                "Applied Sciences",
                "Arts and Design",
                "Engineering and the Built Environment",
                "Health Sciences",
                "Management Sciences"
            };

            ViewBag.Residences = db.Residences.ToList();

            return View();
        }

        // POST: Auth/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            // Helper method to reload dropdowns
            void ReloadDropdowns()
            {
                ViewBag.Faculties = new[]
                {
            "Accounting and Informatics",
            "Applied Sciences",
            "Arts and Design",
            "Engineering and the Built Environment",
            "Health Sciences",
            "Management Sciences"
        };
                ViewBag.Residences = db.Residences.ToList();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email already exists in Students or Staff
                    var existingStudent = db.Students.FirstOrDefault(s => s.Email == model.Email);
                    var existingStaff = db.Staffs.FirstOrDefault(s => s.Email == model.Email);

                    if (existingStudent != null || existingStaff != null)
                    {
                        ModelState.AddModelError("Email", "A user with this email already exists.");
                        ReloadDropdowns();
                        return View(model);
                    }

                    if (model.UserType == "Student")
                    {
                        // Validate student-specific fields
                        if (string.IsNullOrEmpty(model.StudentNumber))
                        {
                            ModelState.AddModelError("StudentNumber", "Student number is required");
                            ReloadDropdowns();
                            return View(model);
                        }

                        // Check if student number already exists
                        if (db.Students.Any(s => s.StudentNumber == model.StudentNumber))
                        {
                            ModelState.AddModelError("StudentNumber", "This student number is already registered.");
                            ReloadDropdowns();
                            return View(model);
                        }

                        var student = new Student
                        {
                            StudentNumber = model.StudentNumber,
                            FirstName = model.FirstName,
                            LastName = model.LastName,
                            Email = model.Email,
                            PasswordHash = model.Password + "_hashed",
                            PhoneNumber = model.PhoneNumber,
                            Faculty = model.Faculty,
                            YearOfStudy = model.YearOfStudy.Value,
                            Gender = model.Gender,
                            DateRegistered = DateTime.Now,
                            IsActive = true
                        };

                        db.Students.Add(student);
                        db.SaveChanges();

                        TempData["SuccessMessage"] = "Student registration successful! Please login with your credentials.";
                        return RedirectToAction("StudentLogin");
                    }
                    else if (model.UserType == "Staff")
                    {
                        if (string.IsNullOrEmpty(model.StaffNumber))
                        {
                            ModelState.AddModelError("StaffNumber", "Staff number is required");
                            ReloadDropdowns();
                            return View(model);
                        }
                        if (string.IsNullOrEmpty(model.Role))
                        {
                            ModelState.AddModelError("Role", "Role is required");
                            ReloadDropdowns();
                            return View(model);
                        }

                        var allowedRoles = new[] { "Admin", "Building Manager", "Security" };
                        if (!allowedRoles.Contains(model.Role))
                        {
                            ModelState.AddModelError("Role", "Please select a valid role");
                            ReloadDropdowns();
                            return View(model);
                        }
                        if ((model.Role == "Building Manager" || model.Role == "Security") && !model.ResidenceID.HasValue)
                        {
                            ModelState.AddModelError("ResidenceID", "Please select a residence for this staff member.");
                            ReloadDropdowns();
                            return View(model);
                        }

                        if (db.Staffs.Any(s => s.StaffNumber == model.StaffNumber))
                        {
                            ModelState.AddModelError("StaffNumber", "This staff number is already registered.");
                            ReloadDropdowns();
                            return View(model);
                        }

                        var staff = new Staff
                        {
                            StaffNumber = model.StaffNumber,
                            FirstName = model.FirstName,
                            LastName = model.LastName,
                            Email = model.Email,
                            Password = model.Password,
                            PhoneNumber = model.PhoneNumber,
                            Role = model.Role,
                            DateRegistered = DateTime.Now,
                            IsActive = true,
                            ResidenceID = (model.Role == "Building Manager" || model.Role == "Security") ? model.ResidenceID : null
                        };

                        db.Staffs.Add(staff);
                        db.SaveChanges();

                        Session["StaffID"] = staff.StaffID;
                        Session["UserType"] = staff.Role;
                        TempData["SuccessMessage"] = "Staff registration successful! Please login with your credentials.";
                        return RedirectToAction("StaffLogin");
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Registration Error: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                    ModelState.AddModelError("", "Registration failed: " + ex.Message);
                    ReloadDropdowns();
                }
            }

            // Reload dropdowns if validation fails
            ReloadDropdowns();
            return View(model);
        }


        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("StudentLogin", "Auth");
        }
    }
}