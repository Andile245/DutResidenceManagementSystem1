// Services/NotificationService.cs
using DUTResManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace DUTResSystemWebApp.Services
{
    public class NotificationService
    {
        public NotificationService()
        {
        }

        public int GetUnreadAnnouncementCount(int studentId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student == null) return 0;

                var relevantAnnouncements = db.Announcements
                    .Where(a => (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now) &&
                               ((a.Staff.Role == "Admin" && (a.TargetAudience == "Everyone" || a.TargetAudience == "Students")) ||
                                (a.Staff.Role == "Building Manager" && a.ResidenceID == student.ResidenceID)))
                    .Select(a => a.AnnouncementID)
                    .ToList();

                if (!relevantAnnouncements.Any()) return 0;

                var viewedAnnouncements = db.StudentAnnouncementViews
                    .Where(v => v.StudentID == studentId && relevantAnnouncements.Contains(v.AnnouncementID))
                    .Select(v => v.AnnouncementID)
                    .ToList();

                return relevantAnnouncements.Count - viewedAnnouncements.Count;
            }
        }

        public List<Announcement> GetUnreadAnnouncements(int studentId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student == null) return new List<Announcement>();

                var relevantAnnouncements = db.Announcements
                    .Include("Staff")
                    .Where(a => (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now) &&
                               ((a.Staff.Role == "Admin" && (a.TargetAudience == "Everyone" || a.TargetAudience == "Students")) ||
                                (a.Staff.Role == "Building Manager" && a.ResidenceID == student.ResidenceID)))
                    .ToList();

                var viewedAnnouncements = db.StudentAnnouncementViews
                    .Where(v => v.StudentID == studentId)
                    .Select(v => v.AnnouncementID)
                    .ToList();

                return relevantAnnouncements
                    .Where(a => !viewedAnnouncements.Contains(a.AnnouncementID))
                    .OrderByDescending(a => a.DatePosted)
                    .ToList();
            }
        }

        public void MarkAnnouncementAsRead(int studentId, int announcementId)
        {
            using (var db = new ResContext())
            {
                var existingView = db.StudentAnnouncementViews
                    .FirstOrDefault(v => v.StudentID == studentId && v.AnnouncementID == announcementId);

                if (existingView == null)
                {
                    var view = new StudentAnnouncementView
                    {
                        StudentID = studentId,
                        AnnouncementID = announcementId,
                        DateViewed = DateTime.Now
                    };
                    db.StudentAnnouncementViews.Add(view);
                    db.SaveChanges();
                }
            }
        }

        public void MarkAllAnnouncementsAsRead(int studentId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student == null) return;

                var relevantAnnouncements = db.Announcements
                    .Where(a => (a.ExpiryDate == null || a.ExpiryDate > DateTime.Now) &&
                               ((a.Staff.Role == "Admin" && (a.TargetAudience == "Everyone" || a.TargetAudience == "Students")) ||
                                (a.Staff.Role == "Building Manager" && a.ResidenceID == student.ResidenceID)))
                    .Select(a => a.AnnouncementID)
                    .ToList();

                foreach (var announcementId in relevantAnnouncements)
                {
                    var existingView = db.StudentAnnouncementViews
                        .FirstOrDefault(v => v.StudentID == studentId && v.AnnouncementID == announcementId);

                    if (existingView == null)
                    {
                        var view = new StudentAnnouncementView
                        {
                            StudentID = studentId,
                            AnnouncementID = announcementId,
                            DateViewed = DateTime.Now
                        };
                        db.StudentAnnouncementViews.Add(view);
                    }
                }
                db.SaveChanges();
            }
        }

        public void CreateNotification(int userId, string userType, string title, string message,
                                     string notificationType, int? relatedId = null, string relatedType = null)
        {
            using (var db = new ResContext())
            {
                var notification = new Notification
                {
                    UserID = userId,
                    UserType = userType,
                    Title = title,
                    Message = message,
                    NotificationType = notificationType,
                    RelatedID = relatedId,
                    RelatedType = relatedType,
                    IsRead = false,
                    DateCreated = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(7)
                };

                db.Notifications.Add(notification);
                db.SaveChanges();
            }
        }

        public int GetUnreadNotificationCount(int userId, string userType)
        {
            using (var db = new ResContext())
            {
                return db.Notifications
                    .Count(n => n.UserID == userId &&
                               n.UserType == userType &&
                               (n.IsRead == null || n.IsRead == false) &&
                               (n.ExpiryDate == null || n.ExpiryDate > DateTime.Now));
            }
        }

        public List<Notification> GetUnreadNotifications(int userId, string userType, int count = 10)
        {
            using (var db = new ResContext())
            {
                return db.Notifications
                    .Where(n => n.UserID == userId &&
                               n.UserType == userType &&
                               (n.IsRead == null || n.IsRead == false) &&
                               (n.ExpiryDate == null || n.ExpiryDate > DateTime.Now))
                    .OrderByDescending(n => n.DateCreated)
                    .Take(count)
                    .ToList();
            }
        }

        public List<Notification> GetAllNotifications(int userId, string userType, int count = 20)
        {
            using (var db = new ResContext())
            {
                return db.Notifications
                    .Where(n => n.UserID == userId && n.UserType == userType)
                    .OrderByDescending(n => n.DateCreated)
                    .Take(count)
                    .ToList();
            }
        }

        public void MarkNotificationAsRead(int notificationId)
        {
            using (var db = new ResContext())
            {
                var notification = db.Notifications.Find(notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    db.SaveChanges();
                }
            }
        }

        public void MarkAllNotificationsAsRead(int userId, string userType)
        {
            using (var db = new ResContext())
            {
                var unreadNotifications = db.Notifications
                    .Where(n => n.UserID == userId &&
                               n.UserType == userType &&
                               (n.IsRead == null || n.IsRead == false))
                    .ToList();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }
                db.SaveChanges();
            }
        }

        public void NotifyBuildingManagerMaintenance(int maintenanceId)
        {
            using (var db = new ResContext())
            {
                var maintenance = db.Maintenances
                    .Include(m => m.Student)
                    .Include(m => m.Student.Residence)
                    .FirstOrDefault(m => m.MaintenanceID == maintenanceId);

                if (maintenance?.Student?.ResidenceID != null)
                {
                    var buildingManager = db.Staffs
                        .Where(s => s.ResidenceID == maintenance.Student.ResidenceID &&
                                    s.Role == "Building Manager")
                        .AsEnumerable()
                        .FirstOrDefault(s => s.IsActive);

                    if (buildingManager != null)
                    {
                        CreateNotification(
                            buildingManager.StaffID,
                            "BuildingManager",
                            "New Maintenance Request",
                            $"Student {maintenance.Student.FirstName} {maintenance.Student.LastName} reported a maintenance issue in {maintenance.Student.Residence?.Name}",
                            "MaintenanceReport",
                            maintenanceId,
                            "Maintenance"
                        );
                    }
                }
            }
        }

        public void NotifyBuildingManagerStudentAllocation(int studentId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students
                    .Include(s => s.Residence)
                    .FirstOrDefault(s => s.StudentID == studentId);

                if (student?.ResidenceID != null)
                {
                    var buildingManager = db.Staffs
                        .Where(s => s.ResidenceID == student.ResidenceID &&
                                    s.Role == "Building Manager")
                        .AsEnumerable()
                        .FirstOrDefault(s => s.IsActive);

                    if (buildingManager != null)
                    {
                        CreateNotification(
                            buildingManager.StaffID,
                            "BuildingManager",
                            "New Student Allocation",
                            $"Student {student.FirstName} {student.LastName} has been allocated to {student.Residence?.Name}",
                            "StudentAllocation",
                            studentId,
                            "Student"
                        );
                    }
                }
            }
        }

        public void NotifyStudentMaintenanceResolved(int maintenanceId)
        {
            using (var db = new ResContext())
            {
                var maintenance = db.Maintenances
                    .Include(m => m.Student)
                    .FirstOrDefault(m => m.MaintenanceID == maintenanceId);

                if (maintenance?.StudentID != null)
                {
                    CreateNotification(
                        maintenance.StudentID,
                        "Student",
                        "Maintenance Request Resolved",
                        $"Your maintenance request has been resolved: {maintenance.IssueDescription}",
                        "MaintenanceResolved",
                        maintenanceId,
                        "Maintenance"
                    );
                }
            }
        }

        public void NotifyAdminStudentRegistration(int studentId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student != null)
                {
                    var admins = db.Staffs
                        .Where(s => s.Role == "Admin")
                        .AsEnumerable()
                        .Where(s => s.IsActive)
                        .ToList();

                    foreach (var admin in admins)
                    {
                        CreateNotification(
                            admin.StaffID,
                            "Admin",
                            "New Student Registration",
                            $"Student {student.FirstName} {student.LastName} ({student.StudentNumber}) has registered in the system",
                            "StudentRegistration",
                            studentId,
                            "Student"
                        );
                    }
                }
            }
        }

        public void NotifyBuildingManagerAdminAnnouncement(int announcementId)
        {
            using (var db = new ResContext())
            {
                var announcement = db.Announcements
                    .Include(a => a.Staff)
                    .FirstOrDefault(a => a.AnnouncementID == announcementId);

                if (announcement?.Staff?.Role == "Admin" &&
                    (announcement.TargetAudience == "Everyone" || announcement.TargetAudience == "Students"))
                {
                    var buildingManagers = db.Staffs
                        .Where(s => s.Role == "Building Manager")
                        .AsEnumerable()
                        .Where(s => s.IsActive)
                        .ToList();

                    foreach (var manager in buildingManagers)
                    {
                        CreateNotification(
                            manager.StaffID,
                            "BuildingManager",
                            "New University Announcement",
                            $"New announcement from Administration: {announcement.Title}",
                            "AdminAnnouncement",
                            announcementId,
                            "Announcement"
                        );
                    }
                }
            }
        }

        public void NotifyStudentWelcome(int studentId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student != null)
                {
                    CreateNotification(
                        student.StudentID,
                        "Student",
                        "Welcome to DUT Residences!",
                        $"Welcome {student.FirstName}! Your student account has been created successfully. You can now apply for residence accommodation.",
                        "Welcome",
                        studentId,
                        "Student"
                    );

                    SendWelcomeEmail(student);
                }
            }
        }

        // FIX: Now creates the notification AND sends the allocation email
        public void NotifyStudentResidenceAllocation(int studentId, int residenceId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                var residence = db.Residences.Find(residenceId);

                if (student != null && residence != null)
                {
                    // Create in-app notification
                    CreateNotification(
                        student.StudentID,
                        "Student",
                        "Residence Allocation",
                        $"You have been allocated to {residence.Name}. Please check your profile for details.",
                        "ResidenceAllocation",
                        residenceId,
                        "Residence"
                    );

                    // FIX: Send the allocation email
                    SendResidenceAllocationEmail(student, residence);
                }
            }
        }

        // FIX: Now creates the notification AND sends the deallocation email
        public void NotifyStudentResidenceDeallocation(int studentId, string residenceName)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student != null)
                {
                    CreateNotification(
                        student.StudentID,
                        "Student",
                        "Residence Deallocation",
                        $"You have been removed from {residenceName}. Please contact administration if this is an error.",
                        "ResidenceDeallocation",
                        studentId,
                        "Student"
                    );

                    // FIX: Send the deallocation email
                    SendResidenceDeallocationEmail(student, residenceName);
                }
            }
        }

        public void NotifyStudentRoomAllocation(int studentId, int roomId)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Include(s => s.Residence).FirstOrDefault(s => s.StudentID == studentId);
                var room = db.Rooms.Find(roomId);

                if (student != null && room != null)
                {
                    CreateNotification(
                        student.StudentID,
                        "Student",
                        "Room Allocation",
                        $"You have been allocated to Room {room.RoomNumber} in {student.Residence?.Name}. Welcome to your new room!",
                        "RoomAllocation",
                        roomId,
                        "Room"
                    );

                    // Send email
                    SendEmail(
                        student.Email,
                        "DUT Room Allocation Confirmation",
                        $@"Dear {student.FirstName} {student.LastName},

You have been successfully allocated to a room in your residence.

Room Details:
- Student Number : {student.StudentNumber}
- Residence      : {student.Residence?.Name}
- Room Number    : {room.RoomNumber}
- Room Type      : {room.RoomType}
- Floor          : {(room.Floor.HasValue ? room.Floor.ToString() : "Not specified")}
- Allocation Date: {DateTime.Now:dd MMMM yyyy}

Please log in to the DUT Residence System to view your full room details.

If you believe this allocation was made in error, please contact your building manager immediately.

Best regards"
                    );
                }
            }
        }

        public void NotifyStudentRoomDeallocation(int studentId, string roomNumber)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Include(s => s.Residence).FirstOrDefault(s => s.StudentID == studentId);
                if (student != null)
                {
                    CreateNotification(
                        student.StudentID,
                        "Student",
                        "Room Deallocation",
                        $"You have been removed from Room {roomNumber}. Please contact your building manager for assistance.",
                        "RoomDeallocation",
                        studentId,
                        "Student"
                    );

                    // Send email
                    SendEmail(
                        student.Email,
                        "DUT Room Deallocation Notice",
                        $@"Dear {student.FirstName} {student.LastName},

This is to inform you that you have been removed from your room.

Deallocation Details:
- Student Number : {student.StudentNumber}
- Residence      : {student.Residence?.Name}
- Room Removed   : {roomNumber}
- Date           : {DateTime.Now:dd MMMM yyyy}

If you believe this was done in error, please contact your building manager as soon as possible.

Best regards"
                    );
                }
            }
        }

        public void NotifyStaffWelcome(int staffId)
        {
            using (var db = new ResContext())
            {
                var staff = db.Staffs.Find(staffId);
                if (staff != null)
                {
                    CreateNotification(
                        staff.StaffID,
                        staff.Role,
                        "Welcome to DUT Residence System",
                        $"Welcome {staff.FirstName}! Your staff account has been created successfully.",
                        "Welcome",
                        staffId,
                        "Staff"
                    );

                    SendStaffWelcomeEmail(staff);
                }
            }
        }

        // ---------------------------------------------------------------
        // Room change request email notifications
        // ---------------------------------------------------------------

        public void SendRoomChangeApprovalEmail(Student student, Room currentRoom, string feedback)
        {
            string feedbackSection = !string.IsNullOrEmpty(feedback)
                ? $"\nBuilding Manager Note:\n{feedback}\n"
                : "";

            string roomInfo = currentRoom != null
                ? $"Room {currentRoom.RoomNumber} ({currentRoom.RoomType})"
                : "your current room";

            SendEmail(
                student.Email,
                "Room Change Request — Approved",
                $@"Dear {student.FirstName} {student.LastName},

Your room change request has been approved.

What happens next:
- You have been removed from {roomInfo}.
- You will appear in the unallocated students list.
- The building manager will assign you to a new room shortly.
- You will receive another notification once your new room has been assigned.
{feedbackSection}
Please log in to the DUT Residence System to check your room status.

Best regards"
            );
        }

        public void SendRoomChangeDeclinedEmail(Student student, Room currentRoom, string feedback)
        {
            string feedbackSection = !string.IsNullOrEmpty(feedback)
                ? $"\nReason for Decline:\n{feedback}\n"
                : "";

            SendEmail(
                student.Email,
                "Room Change Request — Declined",
                $@"Dear {student.FirstName} {student.LastName},

Unfortunately your room change request has been declined.

Current Room     : Room {currentRoom?.RoomNumber} ({currentRoom?.RoomType})
Date Reviewed    : {DateTime.Now:dd MMMM yyyy}
{feedbackSection}
If you believe this decision is incorrect or you have additional information to provide,
please speak to your building manager directly.

Best regards"
            );
        }

        // ---------------------------------------------------------------
        // Private email helpers
        // ---------------------------------------------------------------

        // Single shared email sender — all emails go through here
        public bool SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(toEmail)) return false;
                var smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
                var smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"];
                var smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
                var fromEmail = ConfigurationManager.AppSettings["SmtpFromEmail"];
                var fromName = ConfigurationManager.AppSettings["SmtpFromName"] ?? "DUT Housing";
                int smtpPort;
                if (String.IsNullOrWhiteSpace(smtpHost) || String.IsNullOrWhiteSpace(smtpUsername) ||
                    String.IsNullOrWhiteSpace(smtpPassword) || String.IsNullOrWhiteSpace(fromEmail) ||
                    !Int32.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out smtpPort)) return false;

                var smtp = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };

                message.To.Add(toEmail);
                smtp.Send(message);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email send failed [{subject}]: {ex.Message}");
                return false;
            }
        }

        private void SendResidenceAllocationEmail(Student student, Residence residence)
        {
            SendEmail(
                student.Email,
                "DUT Residence Allocation Confirmation",
                $@"Dear {student.FirstName} {student.LastName},

Congratulations! You have been successfully allocated to a DUT residence.

Allocation Details:
- Student Number : {student.StudentNumber}
- Residence      : {residence.Name}
- Allocation Date: {DateTime.Now:dd MMMM yyyy}

Please log in to the DUT Residence System to view your full residence details and any next steps required.

If you believe this allocation was made in error, please contact the residence administration immediately.

Best regards"
            );
        }

        private void SendResidenceDeallocationEmail(Student student, string residenceName)
        {
            SendEmail(
                student.Email,
                "DUT Residence Deallocation Notice",
                $@"Dear {student.FirstName} {student.LastName},

This is to inform you that you have been removed from your residence.

Deallocation Details:
- Student Number    : {student.StudentNumber}
- Residence Removed : {residenceName}
- Date              : {DateTime.Now:dd MMMM yyyy}

If you believe this was done in error or require further assistance, please contact the residence administration as soon as possible.

Best regards"
            );
        }

        private void SendWelcomeEmail(Student student)
        {
            SendEmail(
                student.Email,
                "Welcome to DUT Residence System",
                $@"Dear {student.FirstName} {student.LastName},

Welcome to the Durban University of Technology Residence Management System!

Your student account has been successfully created with the following details:
- Student Number: {student.StudentNumber}
- Name          : {student.FirstName} {student.LastName}
- Email         : {student.Email}

Please wait until you are allocated to a residence so that you can access all the system features.

To get started, please log in to the system using your student credentials.

If you have any questions or need assistance, please contact the residence administration.

Best regards"
            );
        }

        private void SendStaffWelcomeEmail(Staff staff)
        {
            string residenceInfo = staff.ResidenceID != null
                ? $"Assigned Residence: {staff.Residence?.Name}"
                : "You have access to all residences";

            SendEmail(
                staff.Email,
                "Welcome to DUT Residence Staff System",
                $@"Dear {staff.FirstName} {staff.LastName},

Welcome to the Durban University of Technology Residence Staff Management System!

Your staff account has been successfully created with the following details:
- Staff Number: {staff.StaffNumber}
- Name        : {staff.FirstName} {staff.LastName}
- Role        : {staff.Role}
- {residenceInfo}

You can now access the staff dashboard to manage residence operations.

Best regards"
            );
        }


        public void SendResidenceChangeApprovalEmail(Student student, Residence currentResidence, string feedback)
        {
            string feedbackSection = !string.IsNullOrEmpty(feedback)
                ? $"\n\nDUT Admin Note:\n{feedback}\n"
                : "";

            string residenceInfo = currentResidence != null
                ? $"Residence: {currentResidence.Name}"
                : "your current residence";

            SendEmail(
                student.Email,
                "Residence Change Request — Approved",
                $@"Dear {student.FirstName} {student.LastName},

Your residence change request has been APPROVED.

What happens next:
- You have been removed from {residenceInfo}.
- You will appear in the unallocated students list.
- The DUT Admin will assign you to a new residence shortly.
- You will receive another notification once your new residence has been assigned.
{feedbackSection}
Please log in to the DUT Residence System to check your residence status.

Best regards,
DUT Residence Management"
            );
        }

        public void SendResidenceChangeDeclinedEmail(Student student, string feedback)
        {
            string feedbackSection = !string.IsNullOrEmpty(feedback)
                ? $"\n\nReason for Decline:\n{feedback}\n"
                : "";

            string residenceName = student.Residence?.Name ?? "your current residence";

            SendEmail(
                student.Email,
                "Residence Change Request — Declined",
                $@"Dear {student.FirstName} {student.LastName},

Unfortunately your residence change request has been DECLINED.

Current Residence: {residenceName}
Date Reviewed    : {DateTime.Now:dd MMMM yyyy}
{feedbackSection}
If you believe this decision is incorrect or you have additional information to provide,
please speak to your DUT Admin directly.

Best regards,
DUT Residence Management"
            );
        }
        public void NotifyStudentVisitorCurfew(int studentId, string visitorName, string residenceName, string visitingHours, DateTime curfewTime)
        {
            using (var db = new ResContext())
            {
                var student = db.Students.Find(studentId);
                if (student == null)
                {
                    return;
                }

                CreateNotification(
                    student.StudentID,
                    "Student",
                    "Visitor Time Is Up",
                    $"Visitor time is up at {residenceName}. {visitorName} is still marked inside after visiting hours ({visitingHours}). Please contact security immediately.",
                    "VisitorCurfew",
                    studentId,
                    "Student"
                );

                SendEmail(
                    student.Email,
                    "DUT Visitor Time Is Up",
                    $@"Dear {student.FirstName} {student.LastName},

Visitor time is up at {residenceName}.

Visitor still inside:
- Visitor Name : {visitorName}
- Visiting Hours: {visitingHours}
- Alert Time    : {curfewTime:dd MMM yyyy HH:mm}

Please contact residence security immediately to resolve the visit.

Best regards,
Sunnydale Admin"
                );
            }
        }
        public void NotifyBuildingManagerComplaint(int complaintId)
        {
            using (var db = new ResContext())
            {
                var complaint = db.Complaints
                    .Include(c => c.Student)
                    .Include(c => c.Student.Residence)
                    .FirstOrDefault(c => c.ComplaintId == complaintId); // Fixed: Use ComplaintId, not Id

                if (complaint?.Student?.ResidenceID != null)
                {
                    var buildingManager = db.Staffs
                        .Where(s => s.ResidenceID == complaint.Student.ResidenceID &&
                                    s.Role == "Building Manager")
                        .AsEnumerable()
                        .FirstOrDefault(s => s.IsActive);

                    if (buildingManager != null)
                    {
                        CreateNotification(
                            buildingManager.StaffID,
                            "BuildingManager",
                            "New Student Complaint",
                            $"Student {complaint.Student.FirstName} {complaint.Student.LastName} submitted a complaint: {complaint.Subject}",
                            "ComplaintReceived",
                            complaintId,
                            "Complaint"
                        );
                    }
                }
            }
        }

        // Fixed method with correct property name
        public void NotifyStudentComplaintUpdated(int complaintId, string status, string response)
        {
            using (var db = new ResContext())
            {
                var complaint = db.Complaints
                    .Include(c => c.Student)
                    .FirstOrDefault(c => c.ComplaintId == complaintId); // Fixed: Use ComplaintId, not Id

                if (complaint?.StudentID != null)
                {
                    CreateNotification(
                        complaint.StudentID ?? 0,
                        "Student",
                        $"Complaint Update: {status}",
                        $"Your complaint '{complaint.Subject}' has been updated to '{status}'. Response: {(response?.Length > 100 ? response.Substring(0, 100) + "..." : response ?? "No response provided")}",
                        "ComplaintUpdated",
                        complaintId,
                        "Complaint"
                    );

                    // Also send email notification
                    SendEmail(
                        complaint.Student.Email,
                        $"DUT Complaint Update - {status}",
                        $@"Dear {complaint.Student.FirstName} {complaint.Student.LastName},

Your complaint has been updated.

Complaint Details:
- Complaint ID: {complaint.ComplaintId}
- Subject: {complaint.Subject}
- Category: {complaint.Category}
- Status: {status}
- Date: {DateTime.Now:dd MMMM yyyy}

Response from Building Manager:
{response ?? "No additional response provided."}

Please log in to the DUT Residence System to view the full details.

Best regards,
DUT Residence Management"
                    );
                }
            }
        }
    }
}
