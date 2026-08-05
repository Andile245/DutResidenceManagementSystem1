using DUTResManagementSystem.Models;
using DUTResManagementSystem.ViewModels;
using DUTResSystemWebApp.Services;
using iTextSharp.text.pdf.qrcode;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace DUTResManagementSystem.Controllers
{
    public class VisitorController : Controller
    {
        private readonly ResContext db = new ResContext();
        private const int VisitingStartHour = 8;
        private const int VisitingEndHour = 24;

        private class ScannedVisitorDetails
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string FullName { get; set; }
            public string DocumentNumber { get; set; }
            public string DocumentType { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public int? Age { get; set; }
            public string Gender { get; set; }
            public string Citizenship { get; set; }
            public bool IsValidSouthAfricanId { get; set; }
        }

        private Staff GetLoggedInSecurity()
        {
            if (Session["StaffID"] == null)
            {
                return null;
            }

            int staffId = (int)Session["StaffID"];
            var staff = db.Staffs.Include("Residence").FirstOrDefault(s => s.StaffID == staffId);

            if (staff == null || staff.Role != "Security" || !staff.ResidenceID.HasValue)
            {
                return null;
            }

            return staff;
        }

        private ActionResult RedirectUnauthorizedSecurity()
        {
            if (Session["StaffID"] == null)
            {
                return RedirectToAction("StaffLogin", "Auth");
            }

            TempData["ErrorMessage"] = "Only residence security users can access visitor management.";
            return RedirectToAction("Dashboard", "Staff");
        }
        private void LogCurrentTime()
        {
            var now = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            System.Diagnostics.Debug.WriteLine($"Local Time: {now} - Hour: {now.Hour}, TimeOfDay: {now.TimeOfDay}");
            System.Diagnostics.Debug.WriteLine($"UTC Time: {utcNow} - Hour: {utcNow.Hour}");
            System.Diagnostics.Debug.WriteLine($"Visiting Hours: {VisitingStartHour}:00 to {VisitingEndHour}:00");
            System.Diagnostics.Debug.WriteLine($"Is Within Hours: {IsWithinVisitingHours(now)}");
        }

        private DateTime GetSouthAfricaTime()
        {
            TimeZoneInfo saTimeZone;
            try
            {
                saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            }
            catch
            {
                try
                {
                    saTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
                }
                catch
                {
                    saTimeZone = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "South Africa Standard Time", "South Africa Standard Time");
                }
            }

            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, saTimeZone);
        }

        private bool IsWithinVisitingHours(DateTime dateTime)
        {
            DateTime saTime = GetSouthAfricaTime();
            int currentHour = saTime.Hour;

            // Visiting hours: 08:00 to 00:00 (entry allowed until 23:59)
            return currentHour >= VisitingStartHour && currentHour < VisitingEndHour;
        }

        private string VisitingHoursLabel()
        {
            return "08:00 to 00:00";
        }

        private ScannedVisitorDetails ExtractScannedVisitorDetails(string scannedData)
        {
            string raw = (scannedData ?? string.Empty).Trim();
            var details = new ScannedVisitorDetails
            {
                DocumentType = DetectDocumentType(raw)
            };

            if (string.IsNullOrWhiteSpace(raw))
            {
                return details;
            }

            var fields = ParseScannedFields(raw);
            string firstName = GetFirstFieldValue(fields, "first", "firstname", "given", "givenname", "names", "forenames");
            string lastName = GetFirstFieldValue(fields, "last", "lastname", "surname", "family", "familyname", "lastnames");
            string fullName = GetFirstFieldValue(fields, "name", "fullname", "fullnames", "holder", "cardholder", "visitorname");

            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
            {
                details.FirstName = firstName;
                details.LastName = lastName;
                details.FullName = (details.FirstName + " " + details.LastName).Trim();
            }
            else if (!string.IsNullOrWhiteSpace(fullName))
            {
                details.FullName = fullName;
                SplitFullName(details);
            }

            details.DocumentNumber = GetFirstFieldValue(
                fields,
                "id", "idnumber", "idno", "identity", "identitynumber", "identityno",
                "passport", "passportnumber", "passportno",
                "license", "licence", "licensenumber", "licencenumber", "licenseno", "licenceno",
                "student", "studentnumber", "studentno", "studentid", "card", "cardnumber", "cardno");

            string dateOfBirthValue = GetFirstFieldValue(fields, "dob", "dateofbirth", "birthdate", "birth");
            DateTime scannedBirthDate;
            if (!string.IsNullOrWhiteSpace(dateOfBirthValue) && DateTime.TryParse(dateOfBirthValue, out scannedBirthDate))
            {
                details.DateOfBirth = scannedBirthDate;
                details.Age = CalculateAge(scannedBirthDate);
            }

            if (string.IsNullOrWhiteSpace(details.DocumentNumber))
            {
                Match saIdMatch = Regex.Match(raw, @"\b\d{13}\b");
                if (saIdMatch.Success)
                {
                    details.DocumentNumber = saIdMatch.Value;
                    details.DocumentType = "South African ID";
                }
            }

            if (string.IsNullOrWhiteSpace(details.DocumentNumber))
            {
                Match fallbackMatch = Regex.Match(raw, @"\b[A-Z0-9][A-Z0-9\-\/]{5,39}\b", RegexOptions.IgnoreCase);
                if (fallbackMatch.Success)
                {
                    details.DocumentNumber = fallbackMatch.Value;
                }
            }

            details.DocumentNumber = CleanScannedValue(details.DocumentNumber).ToUpperInvariant();

            PopulateSouthAfricanIdDetails(details);

            if (string.IsNullOrWhiteSpace(details.FullName))
            {
                details.FullName = (details.DocumentType == "South African ID" ? "SA ID Visitor " : "Scanned Visitor ")
                    + (details.DocumentNumber.Length > 4
                    ? details.DocumentNumber.Substring(details.DocumentNumber.Length - 4)
                    : details.DocumentNumber);
            }

            return details;
        }

        private void SplitFullName(ScannedVisitorDetails details)
        {
            if (details == null || string.IsNullOrWhiteSpace(details.FullName))
            {
                return;
            }

            string[] nameParts = details.FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length == 0)
            {
                return;
            }

            details.FirstName = nameParts[0];
            details.LastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : string.Empty;
        }

        private void PopulateSouthAfricanIdDetails(ScannedVisitorDetails details)
        {
            if (details == null || string.IsNullOrWhiteSpace(details.DocumentNumber))
            {
                return;
            }

            string idNumber = Regex.Replace(details.DocumentNumber, @"\D", string.Empty);
            if (!Regex.IsMatch(idNumber, @"^\d{13}$"))
            {
                return;
            }

            details.DocumentNumber = idNumber;
            details.DocumentType = "South African ID";
            details.IsValidSouthAfricanId = IsValidSouthAfricanIdNumber(idNumber);

            int year = int.Parse(idNumber.Substring(0, 2));
            int month = int.Parse(idNumber.Substring(2, 2));
            int day = int.Parse(idNumber.Substring(4, 2));
            int currentTwoDigitYear = GetSouthAfricaTime().Year % 100;
            int century = year <= currentTwoDigitYear ? 2000 : 1900;

            DateTime birthDate;
            if (DateTime.TryParse($"{century + year:D4}-{month:D2}-{day:D2}", out birthDate))
            {
                details.DateOfBirth = birthDate;
                details.Age = CalculateAge(birthDate);
            }

            int genderCode = int.Parse(idNumber.Substring(6, 4));
            details.Gender = genderCode >= 5000 ? "Male" : "Female";
            details.Citizenship = idNumber.Substring(10, 1) == "0" ? "South African citizen" : "Permanent resident";
        }

        private bool IsValidSouthAfricanIdNumber(string idNumber)
        {
            if (!Regex.IsMatch(idNumber ?? string.Empty, @"^\d{13}$"))
            {
                return false;
            }

            int sum = 0;
            bool doubleDigit = false;

            for (int i = idNumber.Length - 1; i >= 0; i--)
            {
                int digit = idNumber[i] - '0';
                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                sum += digit;
                doubleDigit = !doubleDigit;
            }

            return sum % 10 == 0;
        }

        private int CalculateAge(DateTime birthDate)
        {
            DateTime today = GetSouthAfricaTime().Date;
            int age = today.Year - birthDate.Year;

            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        private Dictionary<string, string> ParseScannedFields(string scannedData)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddJsonScannedFields(fields, scannedData);

            string[] parts = Regex.Split(scannedData, @"[\r\n;|]+");

            foreach (string part in parts)
            {
                string value = part.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                Match match = Regex.Match(value, @"^\s*([A-Za-z0-9 _\-]{2,40})\s*[:=]\s*(.+?)\s*$");
                if (!match.Success)
                {
                    continue;
                }

                string key = NormalizeScannedFieldKey(match.Groups[1].Value);
                fields[key] = CleanScannedValue(match.Groups[2].Value);
            }

            return fields;
        }

        private void AddJsonScannedFields(Dictionary<string, string> fields, string scannedData)
        {
            string value = (scannedData ?? string.Empty).Trim();
            if (!value.StartsWith("{") || !value.EndsWith("}"))
            {
                return;
            }

            try
            {
                var json = JObject.Parse(value);
                foreach (var property in json.Properties())
                {
                    if (property.Value == null)
                    {
                        continue;
                    }

                    string key = NormalizeScannedFieldKey(property.Name);
                    string fieldValue = CleanScannedValue(property.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(fieldValue))
                    {
                        fields[key] = fieldValue;
                    }
                }
            }
            catch
            {
            }
        }

        private string NormalizeScannedFieldKey(string key)
        {
            return Regex.Replace(key ?? string.Empty, @"[^A-Za-z0-9]", string.Empty).ToLowerInvariant();
        }

        private string GetFirstFieldValue(Dictionary<string, string> fields, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (fields.ContainsKey(key) && !string.IsNullOrWhiteSpace(fields[key]))
                {
                    return fields[key];
                }
            }

            return null;
        }

        private string DetectDocumentType(string scannedData)
        {
            string value = (scannedData ?? string.Empty).ToLowerInvariant();

            if (value.Contains("student"))
            {
                return "Student Card";
            }

            if (value.Contains("licence") || value.Contains("license") || value.Contains("driver"))
            {
                return "Driver License";
            }

            if (value.Contains("passport"))
            {
                return "Passport";
            }

            return "South African ID";
        }

        private string CleanScannedValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }

        private List<SelectListItem> BuildStudentOptions(int residenceId)
        {
            var roomMap = db.Rooms
                .Where(r => r.ResidenceID == residenceId)
                .ToDictionary(r => r.RoomID, r => r.RoomNumber);

            return db.Students
                .Where(s => s.ResidenceID == residenceId)
                .OrderBy(s => s.FirstName)
                .ThenBy(s => s.LastName)
                .AsEnumerable()
                .Select(s => new SelectListItem
                {
                    Value = s.StudentID.ToString(),
                    Text = s.FirstName + " " + s.LastName + " (" + s.StudentNumber + ")"
                        + (s.RoomID.HasValue && roomMap.ContainsKey(s.RoomID.Value)
                            ? " - Room " + roomMap[s.RoomID.Value]
                            : " - No room")
                })
                .ToList();
        }

        private string GetCurrentStatus(Visitor visitor)
        {
            if (visitor.CheckOutTime.HasValue)
            {
                return "Exited";
            }

            if (visitor.IsActive && visitor.EntryTime.HasValue)
            {
                if (GetSouthAfricaTime().TimeOfDay >= TimeSpan.FromHours(VisitingEndHour))
                {
                    return "Curfew Reached";
                }

                return "Inside";
            }

            return "Pending Entry";
        }

        private VisitorRecordViewModel BuildVisitorRecord(
            Visitor visitor,
            Dictionary<int, string> studentDisplayMap,
            Dictionary<int, string> studentRoomMap)
        {
            return new VisitorRecordViewModel
            {
                VisitorID = visitor.VisitorID,
                FullName = visitor.FullName,
                IDNumber = visitor.IDNumber,
                DocumentType = visitor.DocumentType,
                StudentDisplayName = studentDisplayMap.ContainsKey(visitor.StudentID)
                    ? studentDisplayMap[visitor.StudentID]
                    : "Student not found",
                RoomNumber = studentRoomMap.ContainsKey(visitor.StudentID)
                    ? studentRoomMap[visitor.StudentID]
                    : "Not allocated",
                PassCreatedAt = visitor.CheckInTime,
                EntryTime = visitor.EntryTime,
                ExitTime = visitor.CheckOutTime,
                IsActive = visitor.IsActive,
                CurrentStatus = GetCurrentStatus(visitor),
                QRCode = visitor.QRCode,
                QrCodeSvg = !visitor.CheckOutTime.HasValue ? BuildQrSvg(BuildVisitorScanUrl(visitor.QRCode)) : null
            };
        }

        private List<VisitorRecordViewModel> BuildVisitorRecords(int residenceId)
        {
            var visitors = db.Visitors
                .Where(v => v.ResidenceID == residenceId)
                .OrderByDescending(v => v.CheckInTime)
                .ToList();

            var students = db.Students
                .Where(s => s.ResidenceID == residenceId)
                .Select(s => new
                {
                    s.StudentID,
                    s.FirstName,
                    s.LastName,
                    s.StudentNumber,
                    s.RoomID
                })
                .ToList();

            var roomIds = students
                .Where(s => s.RoomID.HasValue)
                .Select(s => s.RoomID.Value)
                .Distinct()
                .ToList();

            var roomMap = db.Rooms
                .Where(r => roomIds.Contains(r.RoomID))
                .ToDictionary(r => r.RoomID, r => r.RoomNumber);

            var studentDisplayMap = students.ToDictionary(
                s => s.StudentID,
                s => s.FirstName + " " + s.LastName + " (" + s.StudentNumber + ")");

            var studentRoomMap = students.ToDictionary(
                s => s.StudentID,
                s => s.RoomID.HasValue && roomMap.ContainsKey(s.RoomID.Value)
                    ? roomMap[s.RoomID.Value]
                    : "Not allocated");

            return visitors
                .Select(v => BuildVisitorRecord(v, studentDisplayMap, studentRoomMap))
                .ToList();
        }

        private string GenerateVisitorToken(Staff staff, int studentId)
        {
            return "VIS-" + (staff.ResidenceID ?? 0).ToString("D3") + "-" + studentId.ToString("D5") + "-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private string BuildVisitorScanUrl(string qrCode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
            {
                return null;
            }

            string scanPath = Url.Action("Scan", "Visitor", new { token = qrCode });
            string configuredBaseUrl = ConfigurationManager.AppSettings["VisitorQrBaseUrl"];

            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                return configuredBaseUrl.TrimEnd('/') + scanPath;
            }

            if (Request != null && Request.Url != null)
            {
                return Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/') + scanPath;
            }

            return scanPath;
        }

        private string NormalizeScannedQrValue(string qrCode)
        {
            string normalizedQr = (qrCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedQr))
            {
                return string.Empty;
            }

            Uri scannedUri;
            if (Uri.TryCreate(normalizedQr, UriKind.Absolute, out scannedUri))
            {
                string tokenFromQuery = HttpUtility.ParseQueryString(scannedUri.Query)["qrCode"];
                if (string.IsNullOrWhiteSpace(tokenFromQuery))
                {
                    tokenFromQuery = HttpUtility.ParseQueryString(scannedUri.Query)["token"];
                }

                if (!string.IsNullOrWhiteSpace(tokenFromQuery))
                {
                    return tokenFromQuery.Trim();
                }
            }

            return normalizedQr;
        }

        private void SetVisitorScanResult(Visitor visitor, string result, string message, DateTime scanTime)
        {
            var residence = db.Residences.FirstOrDefault(r => r.ResidenceID == visitor.ResidenceID);
            var student = db.Students.FirstOrDefault(s => s.StudentID == visitor.StudentID);
            string roomNumber = "Not allocated";

            if (student != null && student.RoomID.HasValue)
            {
                var room = db.Rooms.FirstOrDefault(r => r.RoomID == student.RoomID.Value);
                if (room != null)
                {
                    roomNumber = room.RoomNumber;
                }
            }

            ViewBag.Result = result;
            ViewBag.Message = message;
            ViewBag.VisitorName = visitor.FullName;
            ViewBag.ResidenceName = residence != null ? residence.Name : "Residence";
            ViewBag.StudentName = student != null ? student.FirstName + " " + student.LastName : "Student host";
            ViewBag.RoomNumber = roomNumber;
            ViewBag.ScanTime = scanTime;
            ViewBag.EntryTime = visitor.EntryTime;
            ViewBag.ExitTime = visitor.CheckOutTime;
            ViewBag.CurrentStatus = GetCurrentStatus(visitor);
        }

        private string BuildQrSvg(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var qrWriter = new QRCodeWriter();
            var matrix = qrWriter.Encode(value, 1, 1);
            int moduleSize = 6;
            int quietZone = 4;
            int width = matrix.GetWidth() + (quietZone * 2);
            int height = matrix.GetHeight() + (quietZone * 2);

            var builder = new StringBuilder();
            builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
                .Append(width * moduleSize)
                .Append(" ")
                .Append(height * moduleSize)
                .Append("\" shape-rendering=\"crispEdges\">");
            builder.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

            for (int y = 0; y < matrix.GetHeight(); y++)
            {
                for (int x = 0; x < matrix.GetWidth(); x++)
                {
                    if (matrix.Get(x, y) == 0)
                    {
                        continue;
                    }

                    builder.Append("<rect x=\"")
                        .Append((x + quietZone) * moduleSize)
                        .Append("\" y=\"")
                        .Append((y + quietZone) * moduleSize)
                        .Append("\" width=\"")
                        .Append(moduleSize)
                        .Append("\" height=\"")
                        .Append(moduleSize)
                        .Append("\" fill=\"#111111\"/>");
                }
            }

            builder.Append("</svg>");
            return builder.ToString();
        }

        private void ProcessCurfewNotifications(Staff staff)
        {
            DateTime saTime = GetSouthAfricaTime();

            if (saTime.TimeOfDay < TimeSpan.FromHours(VisitingEndHour))
            {
                return;
            }

            var overdueVisitors = db.Visitors
                .Where(v => v.ResidenceID == staff.ResidenceID.Value
                    && v.IsActive
                    && v.EntryTime.HasValue
                    && !v.CheckOutTime.HasValue
                    && !v.CurfewAlertSent)
                .ToList();

            if (!overdueVisitors.Any())
            {
                return;
            }

            var notificationService = new NotificationService();
            string residenceName = staff.Residence != null ? staff.Residence.Name : "your residence";

            foreach (var visitor in overdueVisitors)
            {
                notificationService.NotifyStudentVisitorCurfew(visitor.StudentID, visitor.FullName, residenceName, VisitingHoursLabel(), saTime);
                visitor.CurfewAlertSent = true;
            }

            db.SaveChanges();
        }

        private List<VisitorRecordViewModel> FilterVisitorHistory(
            List<VisitorRecordViewModel> records,
            string search,
            string status,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            IEnumerable<VisitorRecordViewModel> filtered = records;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim().ToLowerInvariant();
                filtered = filtered.Where(v =>
                    v.FullName.ToLowerInvariant().Contains(term)
                    || v.IDNumber.ToLowerInvariant().Contains(term)
                    || v.StudentDisplayName.ToLowerInvariant().Contains(term)
                    || v.RoomNumber.ToLowerInvariant().Contains(term)
                    || v.QRCode.ToLowerInvariant().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(v => string.Equals(v.CurrentStatus, status, StringComparison.OrdinalIgnoreCase));
            }

            if (dateFrom.HasValue)
            {
                filtered = filtered.Where(v => v.PassCreatedAt.Date >= dateFrom.Value.Date);
            }

            if (dateTo.HasValue)
            {
                filtered = filtered.Where(v => v.PassCreatedAt.Date <= dateTo.Value.Date);
            }

            return filtered.ToList();
        }

        public ActionResult Dashboard()
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            // Debug time
            LogCurrentTime();

            ProcessCurfewNotifications(staff);

            var allRecords = BuildVisitorRecords(staff.ResidenceID.Value);
            var activeVisitors = allRecords
                .Where(v => v.IsActive && v.EntryTime.HasValue && !v.ExitTime.HasValue)
                .OrderByDescending(v => v.EntryTime)
                .ToList();

            var openPasses = allRecords
                .Where(v => !v.ExitTime.HasValue)
                .OrderByDescending(v => v.PassCreatedAt)
                .ToList();

            var model = new VisitorDashboardViewModel
            {
                ResidenceName = staff.Residence != null ? staff.Residence.Name : "Assigned Residence",
                ActiveVisitorCount = activeVisitors.Count,
                PendingEntryCount = openPasses.Count(v => !v.EntryTime.HasValue),
                TotalVisitsToday = allRecords.Count(v => v.PassCreatedAt >= DateTime.Today),
                ExitsToday = allRecords.Count(v => v.ExitTime.HasValue && v.ExitTime.Value >= DateTime.Today),
                UniqueStudentsVisitedToday = allRecords
                    .Where(v => v.PassCreatedAt >= DateTime.Today)
                    .Select(v => v.StudentDisplayName)
                    .Distinct()
                    .Count(),
                CurfewAlertCount = activeVisitors.Count(v => v.CurrentStatus == "Curfew Reached"),
                ActiveVisitors = activeVisitors,
                OpenVisitorPasses = openPasses
            };

            if (TempData["LatestVisitorId"] is int latestVisitorId)
            {
                model.LatestGeneratedVisitor = openPasses.FirstOrDefault(v => v.VisitorID == latestVisitorId)
                    ?? allRecords.FirstOrDefault(v => v.VisitorID == latestVisitorId);
            }

            return View(model);
        }

        public ActionResult CheckIn()
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            var model = new VisitorCheckInViewModel
            {
                Students = BuildStudentOptions(staff.ResidenceID.Value)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult PreviewScannedDocument(string scannedDocumentData)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return Json(new { hasData = false });
            }

            var details = ExtractScannedVisitorDetails(scannedDocumentData);

            return Json(new
            {
                hasData = !string.IsNullOrWhiteSpace(scannedDocumentData),
                fullName = details.FullName,
                firstName = details.FirstName ?? "",
                lastName = details.LastName ?? "",
                documentNumber = details.DocumentNumber,
                documentType = details.DocumentType,
                dateOfBirth = details.DateOfBirth.HasValue ? details.DateOfBirth.Value.ToString("dd MMM yyyy") : "",
                age = details.Age.HasValue ? details.Age.Value.ToString() : "",
                gender = details.Gender ?? "",
                citizenship = details.Citizenship ?? "",
                isValidSouthAfricanId = details.IsValidSouthAfricanId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckIn(VisitorCheckInViewModel model)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            model.Students = BuildStudentOptions(staff.ResidenceID.Value);

            DateTime saTime = GetSouthAfricaTime();
            var scannedDetails = ExtractScannedVisitorDetails(model.ScannedDocumentData);

            ModelState.Remove("Visitor.FullName");
            ModelState.Remove("Visitor.IDNumber");
            ModelState.Remove("Visitor.DocumentType");

            if (string.IsNullOrWhiteSpace(model.ScannedDocumentData))
            {
                ModelState.AddModelError("ScannedDocumentData", "Scan the visitor's ID card, student card, licence, or passport first.");
            }

            if (string.IsNullOrWhiteSpace(scannedDetails.DocumentNumber))
            {
                ModelState.AddModelError("ScannedDocumentData", "The scan did not contain a readable document number.");
            }

            if (!string.IsNullOrWhiteSpace(scannedDetails.DocumentNumber))
            {
                var openVisit = db.Visitors.FirstOrDefault(v =>
                    v.ResidenceID == staff.ResidenceID.Value
                    && v.IDNumber == scannedDetails.DocumentNumber
                    && !v.CheckOutTime.HasValue);

                if (openVisit != null)
                {
                    TempData["LatestVisitorId"] = openVisit.VisitorID;
                    TempData["ErrorMessage"] = openVisit.FullName + " already has an open visitor pass. Use the generated visitor QR code to log entry or exit.";
                    return RedirectToAction("Dashboard");
                }
            }

            if (saTime.Hour < VisitingStartHour || saTime.Hour >= VisitingEndHour)
            {
                ModelState.AddModelError("", $"Visitors may only enter during visiting hours: {VisitingHoursLabel()}. Current SA time: {saTime:HH:mm}");
            }

            bool studentInResidence = db.Students.Any(s => s.StudentID == model.Visitor.StudentID && s.ResidenceID == staff.ResidenceID.Value);
            if (!studentInResidence)
            {
                ModelState.AddModelError("Visitor.StudentID", "Please choose the student being visited from your assigned residence.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var visitor = new Visitor
            {
                FullName = scannedDetails.FullName,
                IDNumber = scannedDetails.DocumentNumber,
                DocumentType = scannedDetails.DocumentType,
                StudentID = model.Visitor.StudentID,
                CheckInTime = saTime,
                EntryTime = null,
                CheckOutTime = null,
                IsActive = false,
                CurfewAlertSent = false,
                IdentityVerified = true,
                IdentityVerifiedAt = saTime,
                IsOverstayFlagged = false,
                OverstayAlertSentAt = null,
                ResidenceID = staff.ResidenceID.Value,
                QRCode = GenerateVisitorToken(staff, model.Visitor.StudentID)
            };

            db.Visitors.Add(visitor);
            db.SaveChanges();

            TempData["SuccessMessage"] = visitor.FullName + " was captured successfully. Give the visitor the generated QR pass; scanning that pass logs entry time and scanning it again logs exit time.";
            TempData["LatestVisitorId"] = visitor.VisitorID;

            return RedirectToAction("Dashboard");
        }

        [AllowAnonymous]
        public ActionResult Scan(string token, string qrCode)
        {
            string normalizedQr = NormalizeScannedQrValue(!string.IsNullOrWhiteSpace(token) ? token : qrCode);
            DateTime saTime = GetSouthAfricaTime();

            if (string.IsNullOrWhiteSpace(normalizedQr))
            {
                ViewBag.Result = "error";
                ViewBag.Message = "Invalid visitor QR code. Please ask security for a fresh visitor pass.";
                ViewBag.ScanTime = saTime;
                return View("ScanResult");
            }

            var visitor = db.Visitors.FirstOrDefault(v => v.QRCode == normalizedQr);
            if (visitor == null)
            {
                ViewBag.Result = "error";
                ViewBag.Message = "This visitor QR code was not found. Please return to security for assistance.";
                ViewBag.ScanTime = saTime;
                return View("ScanResult");
            }

            if (visitor.CheckOutTime.HasValue)
            {
                SetVisitorScanResult(
                    visitor,
                    "already",
                    visitor.FullName + " already exited at " + visitor.CheckOutTime.Value.ToString("dd MMM yyyy HH:mm") + ".",
                    saTime);
                return View("ScanResult");
            }

            if (!visitor.EntryTime.HasValue)
            {
                if (saTime.Hour < VisitingStartHour || saTime.Hour >= VisitingEndHour)
                {
                    SetVisitorScanResult(
                        visitor,
                        "error",
                        $"Entry is closed. Visitors may only enter during visiting hours: {VisitingHoursLabel()}. Current SA time: {saTime:HH:mm}.",
                        saTime);
                    return View("ScanResult");
                }

                visitor.EntryTime = saTime;
                visitor.IsActive = true;
                db.SaveChanges();

                SetVisitorScanResult(
                    visitor,
                    "entry",
                    "Entry logged successfully. Welcome to " + (ViewBag.ResidenceName ?? "the residence") + ".",
                    saTime);
                return View("ScanResult");
            }

            visitor.CheckOutTime = saTime;
            visitor.IsActive = false;
            db.SaveChanges();

            SetVisitorScanResult(
                visitor,
                "exit",
                "Exit logged successfully. Thank you for visiting.",
                saTime);
            return View("ScanResult");
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmScan(string qrCode)
        {
            return Scan(qrCode, qrCode);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessEntryScan(string qrCode)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            DateTime saTime = GetSouthAfricaTime();

            // Check if within visiting hours using SA time
            if (saTime.Hour < VisitingStartHour || saTime.Hour >= VisitingEndHour)
            {
                TempData["ErrorMessage"] = $"Visitors may only enter during visiting hours: {VisitingHoursLabel()}. Current SA time: {saTime:HH:mm}";
                return RedirectToAction("Dashboard");
            }

            string normalizedQr = NormalizeScannedQrValue(qrCode);
            if (string.IsNullOrWhiteSpace(normalizedQr))
            {
                TempData["ErrorMessage"] = "Scan or enter a valid visitor code to log entry.";
                return RedirectToAction("Dashboard");
            }

            var visitor = db.Visitors.FirstOrDefault(v => v.QRCode == normalizedQr && v.ResidenceID == staff.ResidenceID.Value);
            if (visitor == null)
            {
                TempData["ErrorMessage"] = "This QR code does not belong to your residence.";
                return RedirectToAction("Dashboard");
            }

            if (visitor.CheckOutTime.HasValue)
            {
                TempData["ErrorMessage"] = "This visitor pass has already been completed.";
                return RedirectToAction("Dashboard");
            }

            if (visitor.EntryTime.HasValue && visitor.IsActive)
            {
                TempData["ErrorMessage"] = visitor.FullName + " has already been logged in.";
                return RedirectToAction("Dashboard");
            }

            // Use SA time for entry time as well
            visitor.EntryTime = saTime;
            visitor.IsActive = true;
            db.SaveChanges();

            TempData["SuccessMessage"] = visitor.FullName + " has been logged in successfully at " + saTime.ToString("HH:mm");
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessExitScan(string qrCode)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            DateTime saTime = GetSouthAfricaTime();

            string normalizedQr = NormalizeScannedQrValue(qrCode);
            if (string.IsNullOrWhiteSpace(normalizedQr))
            {
                TempData["ErrorMessage"] = "Scan or enter a valid visitor code to log exit.";
                return RedirectToAction("Dashboard");
            }

            var visitor = db.Visitors.FirstOrDefault(v => v.QRCode == normalizedQr && v.ResidenceID == staff.ResidenceID.Value);
            if (visitor == null)
            {
                TempData["ErrorMessage"] = "This QR code does not belong to your residence.";
                return RedirectToAction("Dashboard");
            }

            if (!visitor.EntryTime.HasValue)
            {
                TempData["ErrorMessage"] = "This visitor has not been logged in yet.";
                return RedirectToAction("Dashboard");
            }

            if (visitor.CheckOutTime.HasValue)
            {
                TempData["ErrorMessage"] = "This visitor has already been logged out.";
                return RedirectToAction("Dashboard");
            }

            visitor.CheckOutTime = saTime;
            visitor.IsActive = false;
            db.SaveChanges();

            TempData["SuccessMessage"] = visitor.FullName + " has been logged out successfully at " + saTime.ToString("HH:mm");
            return RedirectToAction("Dashboard");
        }

        public ActionResult ActiveVisitors()
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            ProcessCurfewNotifications(staff);

            var activeVisitors = BuildVisitorRecords(staff.ResidenceID.Value)
                .Where(v => v.IsActive && v.EntryTime.HasValue && !v.ExitTime.HasValue)
                .OrderByDescending(v => v.EntryTime)
                .ToList();

            return View(activeVisitors);
        }

        public ActionResult VisitorHistory(string search, string status, DateTime? dateFrom, DateTime? dateTo)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            var records = FilterVisitorHistory(BuildVisitorRecords(staff.ResidenceID.Value), search, status, dateFrom, dateTo);
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

            return View(records);
        }

        public ActionResult ExportHistory(string search, string status, DateTime? dateFrom, DateTime? dateTo)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            var records = FilterVisitorHistory(BuildVisitorRecords(staff.ResidenceID.Value), search, status, dateFrom, dateTo);
            var csv = new StringBuilder();
            csv.AppendLine("Visitor Name,ID Number,Document Type,Student,Room,Pass Created,Entry Time,Exit Time,Status,Code");

            foreach (var record in records)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(record.FullName),
                    EscapeCsv(record.IDNumber),
                    EscapeCsv(record.DocumentType),
                    EscapeCsv(record.StudentDisplayName),
                    EscapeCsv(record.RoomNumber),
                    EscapeCsv(record.PassCreatedAt.ToString("yyyy-MM-dd HH:mm")),
                    EscapeCsv(record.EntryTime.HasValue ? record.EntryTime.Value.ToString("yyyy-MM-dd HH:mm") : ""),
                    EscapeCsv(record.ExitTime.HasValue ? record.ExitTime.Value.ToString("yyyy-MM-dd HH:mm") : ""),
                    EscapeCsv(record.CurrentStatus),
                    EscapeCsv(record.QRCode)));
            }

            return File(
                Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                "visitor-history-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv");
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public ActionResult PrintablePass(int id)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            var record = BuildVisitorRecords(staff.ResidenceID.Value).FirstOrDefault(v => v.VisitorID == id);
            if (record == null)
            {
                return HttpNotFound();
            }

            return View(record);
        }

        public ActionResult CheckOut(string qrCode)
        {
            TempData["ErrorMessage"] = "Use the exit scanner on the dashboard to log visitors out.";
            return RedirectToAction("Dashboard");
        }

        public ActionResult ManualCheckOut(int id)
        {
            var staff = GetLoggedInSecurity();
            if (staff == null)
            {
                return RedirectUnauthorizedSecurity();
            }

            var visitor = db.Visitors.FirstOrDefault(v => v.VisitorID == id && v.ResidenceID == staff.ResidenceID.Value);
            if (visitor == null)
            {
                TempData["ErrorMessage"] = "Visitor not found in your residence.";
                return RedirectToAction("Dashboard");
            }

            if (!visitor.EntryTime.HasValue)
            {
                TempData["ErrorMessage"] = "This visitor has not been logged in yet.";
                return RedirectToAction("Dashboard");
            }

            if (visitor.CheckOutTime.HasValue)
            {
                TempData["ErrorMessage"] = "This visitor has already been logged out.";
                return RedirectToAction("Dashboard");
            }

            visitor.CheckOutTime = GetSouthAfricaTime();
            visitor.IsActive = false;
            db.SaveChanges();

            TempData["SuccessMessage"] = "Visitor checked out manually.";
            return RedirectToAction("Dashboard");
        }
    }
}
