using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using ReservationSystem.Models;
using ReservationSystem.Services;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

//AI prompts:
//"Design an instructor dashboard using ASP.NET Core Razor Pages that supports: creating single and recurring class reservations, validating inputs, blocking holiday dates using a calendar API, displaying filtered weekly reservations, and sending feedback via email to admins."

//"Build an ASP.NET Core PageModel that handles class reservations for instructors, including checks for public holidays, weekends, date conflicts, and term boundaries. Log each action, send email alerts for blocked reservations, and allow recurring weekly bookings."

//"Create a Razor Page backend in C# (ASP.NET Core) for an instructor reservation panel. The system should support class reservation requests, conflict detection, public holiday blocking (via API), term validation, cancellation and modification requests, and calendar event generation in JSON format for FullCalendar."

namespace ReservationSystem.Pages
{
    [Authorize(Roles = "instructor")] // Only users with the instructor role can access
    public class InstructorPanelModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleCalendarService _calendarService;
        private readonly LoggingService _loggingService;
        private readonly EmailService _emailService;

        public InstructorPanelModel(ApplicationDbContext context, GoogleCalendarService calendarService, LoggingService loggingService, EmailService emailService)
        {
            _context = context;
            _calendarService = calendarService;
            _loggingService = loggingService;
            _emailService = emailService;
        }

        [BindProperty]
        public Reservation ReservationInput { get; set; } = new Reservation { Date = DateTime.Today };

        [BindProperty(SupportsGet = true)]
        public string? SelectedWeek { get; set; }

        [BindProperty]
        public bool ApplyToTerm { get; set; }

        [BindProperty]
        public int ModificationReservationId { get; set; }

        [BindProperty]
        [Display(Name = "New Classroom (Optional)")]
        public int? NewRequestedClassId { get; set; }

        [BindProperty]
        [DataType(DataType.Date)]
        [Display(Name = "New Date (Optional)")]
        public DateTime? NewRequestedDate { get; set; }

        [BindProperty]
        [Display(Name = "New Time Slot (Optional)")]
        public string? NewRequestedTimeSlot { get; set; }

        [BindProperty]
        [StringLength(500, ErrorMessage = "Additional notes cannot exceed 500 characters.")]
        [Display(Name = "Additional Notes / Reason for Change")]
        public string? ModificationRequestNotes { get; set; } = string.Empty;

        public string? UserName { get; set; }
        public int CurrentInstructorId { get; private set; }

        public DateTime? ActiveTermStartDate { get; private set; }
        public DateTime? ActiveTermEndDate { get; private set; }

        [TempData]
        public string? Message { get; set; }
        [TempData]
        public string? MessageType { get; set; }

        public List<Reservation> MyReservations { get; set; } = new List<Reservation>();
        public List<Reservation> WeeklyReservations { get; set; } = new List<Reservation>();
        public List<string> WeekOptions { get; set; } = new List<string>();
        public List<SelectListItem> ClassOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> TimeSlotOptions { get; set; } = new List<SelectListItem>();

        private string GetInstructorIdentifier()
        {
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? "UnknownInstructor";
        }

        private async Task PopulateSelectListsAsync()
        {
            ClassOptions = await _context.Classes
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();

            TimeSlotOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Time Slot --" },
                new SelectListItem { Value = "10:00–11:00", Text = "10:00–11:00" },
                new SelectListItem { Value = "11:00–12:00", Text = "11:00–12:00" },
                new SelectListItem { Value = "12:00–13:00", Text = "12:00–13:00" },
                new SelectListItem { Value = "13:00–14:00", Text = "13:00–14:00" },
                new SelectListItem { Value = "14:00–15:00", Text = "14:00–15:00" },
                new SelectListItem { Value = "15:00–16:00", Text = "15:00–16:00" },
                new SelectListItem { Value = "16:00–17:00", Text = "16:00–17:00" },
                new SelectListItem { Value = "17:00–18:00", Text = "17:00–18:00" }
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var instructorIdString = HttpContext.Session.GetString("UserId");
            UserName = HttpContext.Session.GetString("UserName");
            var instructorIdentifier = GetInstructorIdentifier();

            if (!int.TryParse(instructorIdString, out int uid))
            {
                await _loggingService.LogActionAsync(instructorIdentifier, "View Instructor Panel", "Failure - Invalid UserID", "Could not parse UserId from session.");
                TempData["Message"] = "Invalid session. Please log in again."; MessageType = "danger";
                return RedirectToPage("/Login");
            }
            CurrentInstructorId = uid;
            await _loggingService.LogActionAsync(instructorIdentifier, "View Instructor Panel", "Attempt");

            try
            {
                var activeTerm = await _context.Terms.FirstOrDefaultAsync(t => t.IsActive);
                if (activeTerm != null)
                {
                    ActiveTermStartDate = activeTerm.StartDate;
                    ActiveTermEndDate = activeTerm.EndDate;
                }

                MyReservations = await _context.Reservations
                    .Include(r => r.Class)
                    .Include(r => r.Instructor)
                    .Where(r => r.InstructorId == uid)
                    .OrderByDescending(r => r.Date)
                    .ThenBy(r => r.TimeSlot)
                    .ToListAsync();

                var dates = MyReservations.Select(r => r.Date).Distinct().ToList();
                if (dates.Any())
                {
                    var weekRanges = dates.Select(d => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)).Distinct().OrderBy(w => w).ToList();
                    WeekOptions.Clear();
                    WeekOptions.Add("-- All My Reservations --");
                    foreach (var w in weekRanges)
                    {
                        var yearForWeek = dates.First(d => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday) == w).Year;
                        var startDt = FirstDateOfWeekISO8601(yearForWeek, w);
                        WeekOptions.Add($"{startDt:dd.MM} - {startDt.AddDays(6):dd.MM} (Y{yearForWeek})");
                    }
                    if (!string.IsNullOrEmpty(SelectedWeek) && WeekOptions.Contains(SelectedWeek) && SelectedWeek != "-- All My Reservations --") // Çevrildi
                    {
                        var parts = SelectedWeek.Split(new[] { " - ", " (Y", ")" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 3 && DateTime.TryParseExact(parts[0], "dd.MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd) && DateTime.TryParseExact(parts[1], "dd.MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed) && int.TryParse(parts[2], out int ys))
                        {
                            var sow = new DateTime(ys, sd.Month, sd.Day);
                            WeeklyReservations = MyReservations.Where(r => r.Date >= sow && r.Date < sow.AddDays(7)).ToList();
                        }
                    }
                    else
                    {
                        WeeklyReservations = MyReservations.ToList();
                    }
                }
                await PopulateSelectListsAsync();
                await _loggingService.LogActionAsync(instructorIdentifier, "View Instructor Panel", "Success", $"Loaded {MyReservations.Count} total reservations. Active Term: {activeTerm?.Name ?? "None"}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, "Error loading Instructor Panel data.");
                Message = "An error occurred while loading your panel."; MessageType = "danger";
            }
            return Page();
        }

        public static DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
            DateTime firstMonday = jan1.AddDays(daysOffset);
            if (firstMonday.Year < year || (firstMonday.Year == year && jan1.DayOfWeek > DayOfWeek.Monday && jan1.DayOfWeek <= DayOfWeek.Thursday))
            { /* Correction for year boundary */ } // Çevrildi (Yorum)
            return firstMonday.AddDays((weekOfYear - 1) * 7);
        }

        private string ExtractHolidayNameFromMessage(string instanceCheckResult)
        {
            var match = Regex.Match(instanceCheckResult, @"\(([^)]+) - \d{2}\.\d{2}\.\d{4}\)");
            return (match.Success && match.Groups.Count > 1) ? match.Groups[1].Value.Trim() : "Unspecified Public Holiday";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var instructorIdString = HttpContext.Session.GetString("UserId");
            var instructorIdentifier = GetInstructorIdentifier();

            if (!int.TryParse(instructorIdString, out int uid))
            {
                TempData["Message"] = "Invalid user session."; TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(instructorIdentifier, "Create Reservation Attempt", "Failure - Invalid Session UID");
                await PopulateSelectListsAsync(); return Page();
            }
            CurrentInstructorId = uid;

            await PopulateSelectListsAsync();
            ClearModelStateForOtherForms(nameof(ReservationInput), nameof(ApplyToTerm));
            string logDetailsBase = $"ClassID: {ReservationInput.ClassId}, Date: {ReservationInput.Date:yyyy-MM-dd}, TimeSlot: {ReservationInput.TimeSlot}, ApplyToTerm: {ApplyToTerm}";
            await _loggingService.LogActionAsync(instructorIdentifier, "Create Reservation Attempt", "Processing", logDetailsBase);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Message"] = "Please fill out the reservation form correctly:<br/>" + string.Join("<br/>", errors);
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(instructorIdentifier, "Create Reservation Attempt", "Failure - Invalid Model", $"Errors: {string.Join("; ", errors)}. Details: {logDetailsBase}");
                return Page();
            }

            Term? termForOperation = await _context.Terms.FirstOrDefaultAsync(t => t.IsActive);
            if (termForOperation == null)
            {
                termForOperation = await _context.Terms.FirstOrDefaultAsync(t => ReservationInput.Date >= t.StartDate && ReservationInput.Date <= t.EndDate);
            }

            if (termForOperation == null)
            {
                TempData["Message"] = "No valid academic term found for the selected date, or no active term is defined in the system."; // Çevrildi
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(instructorIdentifier, "Create Reservation Attempt", "Failure - No Valid Term", $"No active or matching term for date {ReservationInput.Date:yyyy-MM-dd}. Details: {logDetailsBase}");
                return Page();
            }
            if (ReservationInput.Date < termForOperation.StartDate || ReservationInput.Date > termForOperation.EndDate)
            {
                TempData["Message"] = $"The date you selected ({ReservationInput.Date:dd.MM.yyyy}) is outside the active academic term ({termForOperation.Name}: {termForOperation.StartDate:dd.MM.yyyy} - {termForOperation.EndDate:dd.MM.yyyy}).";
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(instructorIdentifier, "Create Reservation Attempt", "Failure - Date Out of Term", $"Date {ReservationInput.Date:yyyy-MM-dd} is out of term '{termForOperation.Name}'. Details: {logDetailsBase}");
                return Page();
            }

            User? currentUser = await _context.Users.FindAsync(uid);
            string classNameForEmail = (await _context.Classes.FindAsync(ReservationInput.ClassId))?.Name ?? "Unspecified Class";
            bool changesToSaveToDb = false;

            try
            {
                if (ApplyToTerm)
                {
                    var selectedDayOfWeek = ReservationInput.Date.DayOfWeek;
                    List<Reservation> reservationsToCreate = new List<Reservation>();
                    List<BlockedHolidayAttempt> blockedAttemptsToLog = new List<BlockedHolidayAttempt>();
                    int successCount = 0; int skippedHolidayCount = 0; int skippedWeekendCount = 0; int skippedConflictCount = 0;
                    List<string> holidayNotificationFailures = new List<string>();

                    DateTime currentDate = termForOperation.StartDate > ReservationInput.Date ? termForOperation.StartDate : ReservationInput.Date;
                    if (currentDate < termForOperation.StartDate) currentDate = termForOperation.StartDate;

                    while (currentDate <= termForOperation.EndDate)
                    {
                        if (currentDate.DayOfWeek == selectedDayOfWeek)
                        {
                            string holidayNameForLogAndEmail = "Unspecified Public Holiday";
                            var holidaysForDate = await _calendarService.GetHolidaysAsync(currentDate, currentDate);
                            var holidayDetailForDate = holidaysForDate.FirstOrDefault();
                            if (holidayDetailForDate != null)
                            {
                                holidayNameForLogAndEmail = holidayDetailForDate.Summary ?? holidayNameForLogAndEmail;
                            }

                            string? instanceCheckResult = await CanCreateReservationInstance(uid, ReservationInput.ClassId, currentDate, ReservationInput.TimeSlot, instructorIdentifier, termForOperation);
                            if (string.IsNullOrEmpty(instanceCheckResult))
                            {
                                reservationsToCreate.Add(new Reservation { ClassId = ReservationInput.ClassId, Date = currentDate, TimeSlot = ReservationInput.TimeSlot, UserId = uid, InstructorId = uid, TermId = termForOperation.Id, Status = "Pending" });
                                successCount++;
                            }
                            else
                            {
                                if (instanceCheckResult.Contains("Weekends")) skippedWeekendCount++;
                                else if (instanceCheckResult.Contains("public holiday"))
                                {
                                    skippedHolidayCount++;
                                    holidayNameForLogAndEmail = ExtractHolidayNameFromMessage(instanceCheckResult);

                                    var blockedAttempt = new BlockedHolidayAttempt
                                    {
                                        InstructorId = uid,
                                        ClassId = ReservationInput.ClassId,
                                        RequestedDate = currentDate,
                                        RequestedTimeSlot = ReservationInput.TimeSlot,
                                        HolidayName = holidayNameForLogAndEmail,
                                        AttemptTimestamp = DateTime.UtcNow
                                    };
                                    blockedAttemptsToLog.Add(blockedAttempt);
                                    await _loggingService.LogActionAsync(instructorIdentifier, "Blocked Holiday Attempt Logged (Recurring)", "Success", $"Logged holiday block for {currentDate:yyyy-MM-dd} ({holidayNameForLogAndEmail}). Details: {logDetailsBase}");
                                    changesToSaveToDb = true;

                                    if (currentUser != null && !string.IsNullOrEmpty(currentUser.Email))
                                    {
                                        try
                                        {
                                            string subject = "Your Reservation Attempt and Public Holiday Alert ";
                                            string body = $"Dear {currentUser.FullName},<br/><br/>" + // Çevrildi
                                                          $"Your reservation attempt for classroom {classNameForEmail} on {currentDate:dd.MM.yyyy} at {ReservationInput.TimeSlot} " + // Çevrildi
                                                          $"could not be created because it coincides with the <b>{holidayNameForLogAndEmail} ({currentDate:dd.MM.yyyy})</b> public holiday.<br/>" + // Çevrildi
                                                          $"This attempt has been logged in our system.<br/><br/>" + // Çevrildi
                                                          $"Thank you for your understanding.<br/>Reservation System";
                                            await _emailService.SendEmailAsync(currentUser.Email, subject, body);
                                            await _loggingService.LogActionAsync(instructorIdentifier, "Holiday Block Email Sent (Recurring)", "Success", $"To: {currentUser.Email} for date {currentDate:yyyy-MM-dd}");
                                        }
                                        catch (Exception ex)
                                        {
                                            await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Failed to send holiday block email (recurring) to {currentUser.Email} for date {currentDate:yyyy-MM-dd}");
                                            holidayNotificationFailures.Add($"{currentDate:dd.MM.yyyy} ({holidayNameForLogAndEmail})");
                                        }
                                    }
                                }
                                else skippedConflictCount++;
                            }
                        }
                        currentDate = currentDate.AddDays(1);
                    }

                    if (reservationsToCreate.Any()) { _context.Reservations.AddRange(reservationsToCreate); changesToSaveToDb = true; }
                    if (blockedAttemptsToLog.Any()) { _context.BlockedHolidayAttempts.AddRange(blockedAttemptsToLog); changesToSaveToDb = true; }

                    if (changesToSaveToDb) await _context.SaveChangesAsync();

                    List<string> finalMessageParts = new List<string>();
                    if (successCount > 0) { finalMessageParts.Add($"✅ {successCount} reservation(s) created as 'Pending'."); MessageType = "success"; }
                    else if (!blockedAttemptsToLog.Any() && skippedConflictCount == 0 && skippedHolidayCount == 0 && skippedWeekendCount == 0) { finalMessageParts.Add("⚠️ No new reservations could be created (likely no suitable dates found for the selected day)."); MessageType = "warning"; }

                    if (skippedHolidayCount > 0) finalMessageParts.Add($"🗓️ {skippedHolidayCount} day(s) were skipped due to public holidays (logged, email sent).");
                    if (skippedWeekendCount > 0) finalMessageParts.Add($"🚫 {skippedWeekendCount} day(s) skipped as they were weekends.");
                    if (skippedConflictCount > 0) finalMessageParts.Add($"❗ {skippedConflictCount} day(s) skipped due to conflicts.");
                    if (holidayNotificationFailures.Any()) { finalMessageParts.Add($"📧 Holiday alert emails could not be sent for: {string.Join(", ", holidayNotificationFailures)}"); } // Çevrildi

                    if (finalMessageParts.Any()) TempData["Message"] = string.Join("<br/>", finalMessageParts);
                    else if (successCount == 0 && skippedHolidayCount == 0 && skippedWeekendCount == 0 && skippedConflictCount == 0) TempData["Message"] = "ℹ️ No suitable reservation slots found for the selected day(s).";

                    if (string.IsNullOrEmpty(MessageType) && finalMessageParts.Any()) MessageType = "info";
                    else if (string.IsNullOrEmpty(MessageType)) MessageType = "secondary";

                    await _loggingService.LogActionAsync(instructorIdentifier, "Create Recurring Reservation", MessageType, $"Created: {successCount}, Skipped Holiday: {skippedHolidayCount}, Skipped Weekend: {skippedWeekendCount}, Skipped Conflict: {skippedConflictCount}. Details: {logDetailsBase}");
                    return RedirectToPage();
                }
                else
                {
                    string? singleCheckResult = await CanCreateReservationInstance(uid, ReservationInput.ClassId, ReservationInput.Date, ReservationInput.TimeSlot, instructorIdentifier, termForOperation);

                    if (!string.IsNullOrEmpty(singleCheckResult))
                    {
                        TempData["Message"] = singleCheckResult; TempData["MessageType"] = "danger";
                        await _loggingService.LogActionAsync(instructorIdentifier, "Create Single Reservation", "Failure - CheckInstance Failed", $"Reason: {singleCheckResult}. Details: {logDetailsBase}");
                        if (singleCheckResult.Contains("public holiday"))
                        {
                            string holidayNameForLogAndEmail = ExtractHolidayNameFromMessage(singleCheckResult);
                            var blockedAttempt = new BlockedHolidayAttempt
                            {
                                InstructorId = uid,
                                ClassId = ReservationInput.ClassId,
                                RequestedDate = ReservationInput.Date,
                                RequestedTimeSlot = ReservationInput.TimeSlot,
                                HolidayName = holidayNameForLogAndEmail,
                                AttemptTimestamp = DateTime.UtcNow
                            };
                            _context.BlockedHolidayAttempts.Add(blockedAttempt);
                            await _context.SaveChangesAsync();
                            await _loggingService.LogActionAsync(instructorIdentifier, "Blocked Holiday Attempt Logged (Single)", "Success", $"Logged holiday block for {ReservationInput.Date:yyyy-MM-dd} ({holidayNameForLogAndEmail}). Details: {logDetailsBase}");

                            if (currentUser != null && !string.IsNullOrEmpty(currentUser.Email))
                            {
                                try
                                {
                                    string subject = "Your Reservation Attempt and Public Holiday Alert";
                                    string body = $"Dear {currentUser.FullName},<br/><br/>Your reservation attempt for classroom {classNameForEmail} on {ReservationInput.Date:dd.MM.yyyy} at {ReservationInput.TimeSlot} could not be created because it coincides with the <b>{holidayNameForLogAndEmail} ({ReservationInput.Date:dd.MM.yyyy})</b> public holiday.<br/>This attempt has been logged in our system.<br/><br/>Thank you for your understanding.<br/>Reservation System"; // Çevrildi
                                    await _emailService.SendEmailAsync(currentUser.Email, subject, body);
                                    await _loggingService.LogActionAsync(instructorIdentifier, "Holiday Block Email Sent (Single)", "Success", $"To: {currentUser.Email} for date {ReservationInput.Date:yyyy-MM-dd}");
                                    TempData["Message"] += "<br/>Public holiday alert email sent.";
                                }
                                catch (Exception ex)
                                {
                                    await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Failed to send holiday block email (single) to {currentUser.Email} for date {ReservationInput.Date:yyyy-MM-dd}");
                                    TempData["Message"] += "<br/>Could not send public holiday alert email.";
                                }
                            }
                        }
                        return Page();
                    }

                    ReservationInput.TermId = termForOperation.Id; ReservationInput.UserId = uid; ReservationInput.InstructorId = uid; ReservationInput.Status = "Pending";
                    _context.Reservations.Add(ReservationInput); await _context.SaveChangesAsync();
                    TempData["Message"] = "Your reservation request has been received and is pending approval."; TempData["MessageType"] = "success";
                    await _loggingService.LogActionAsync(instructorIdentifier, "Create Single Reservation", "Success", $"Reservation ID: {ReservationInput.Id} created as Pending. Details: {logDetailsBase}");
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Error during OnPostAsync (Create Reservation). Details: {logDetailsBase}");
                TempData["Message"] = "An error occurred while creating your reservation."; TempData["MessageType"] = "danger";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRequestCancellationAsync(int reservationId)
        {
            var instructorIdentifier = GetInstructorIdentifier();
            var instructorIdString = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(instructorIdString) || !int.TryParse(instructorIdString, out int uid))
            {
                TempData["Message"] = "Your session is invalid. Please log in again."; TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(instructorIdentifier, "Request Cancellation Attempt", "Failure - Invalid Session");
                return RedirectToPage("/Login");
            }
            await _loggingService.LogActionAsync(instructorIdentifier, "Request Cancellation Attempt", "Processing", $"Res ID: {reservationId}");

            try
            {
                var reservationToCancel = await _context.Reservations.Include(r => r.Class).Include(r => r.User).FirstOrDefaultAsync(r => r.Id == reservationId);
                if (reservationToCancel == null)
                {
                    TempData["Message"] = "Reservation not found."; TempData["MessageType"] = "danger";
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Cancellation", "Failure - Not Found", $"Reservation ID {reservationId} not found.");
                    return RedirectToPage();
                }
                if (reservationToCancel.InstructorId != uid)
                {
                    TempData["Message"] = " You are not authorized to cancel this reservation."; TempData["MessageType"] = "danger";
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Cancellation", "Failure - Unauthorized", $"User {uid} not authorized to cancel Res ID {reservationId}. Owner: {reservationToCancel.InstructorId}");
                    return RedirectToPage();
                }

                var termOfReservation = await _context.Terms.FindAsync(reservationToCancel.TermId);
                bool canOperate = false;
                if (termOfReservation != null)
                {
                    canOperate = (reservationToCancel.Status == "Pending" || reservationToCancel.Status == "Approved");
                }

                if (canOperate)
                {
                    reservationToCancel.PreviousStatus = reservationToCancel.Status;
                    reservationToCancel.Status = "Cancellation Requested";
                    await _context.SaveChangesAsync();
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Cancellation", "Success", $"ResId {reservationId}, PreviousStatus: {reservationToCancel.PreviousStatus}, NewStatus: Cancellation Requested");
                    TempData["Message"] = $" Your cancellation request for reservation (ID: {reservationId}) has been sent."; MessageType = "success";

                    var adminUsers = await _context.Users.Where(u => u.Role.ToLower() == "admin").ToListAsync();
                    foreach (var admin in adminUsers)
                    {
                        if (!string.IsNullOrEmpty(admin.Email))
                        {
                            try
                            {
                                await _emailService.SendEmailAsync(admin.Email, $"Reservation Cancellation Request: ID {reservationId}", $"Dear Admin,<br/><br/>Instructor <b>{UserName}</b> (ID: {uid}) has requested the cancellation of their reservation for class <b>{reservationToCancel.Class?.Name}</b> on {reservationToCancel.Date:dd.MM.yyyy} at {reservationToCancel.TimeSlot} (ID: {reservationId}).<br/><br/>Please review the request in the admin panel.<br/><br/>System.");
                                await _loggingService.LogActionAsync(instructorIdentifier, "Cancellation Request Email Sent to Admin", "Success", $"To: {admin.Email} for Res ID {reservationId}");
                            }
                            catch (Exception emailEx)
                            {
                                await _loggingService.LogErrorAsync(emailEx, instructorIdentifier, $"Failed to send cancellation request email to admin {admin.Email} for Res ID {reservationId}");
                            }
                        }
                    }
                }
                else
                {
                    TempData["Message"] = "ℹ This reservation cannot be cancelled (e.g., status is not eligible or it does not belong to a valid term)."; TempData["MessageType"] = "info"; // Çevrildi
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Cancellation", "Failure - Cannot Operate", $"ResId {reservationId} status ({reservationToCancel.Status}) not eligible for cancellation request.");
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Error requesting cancellation for Res ID {reservationId}");
                TempData["Message"] = "An error occurred while creating your cancellation request."; MessageType = "danger"; // Çevrildi
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRequestModificationAsync()
        {
            var instructorIdentifier = GetInstructorIdentifier();
            var instructorIdString = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(instructorIdString) || !int.TryParse(instructorIdString, out int uid))
            {
                TempData["Message"] = "❗ Your session is invalid. Please log in again."; TempData["MessageType"] = "danger"; // Çevrildi
                await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification Attempt", "Failure - Invalid Session");
                return RedirectToPage("/Login");
            }

            await PopulateSelectListsAsync();
            ClearModelStateForOtherForms(nameof(ModificationReservationId), nameof(NewRequestedClassId), nameof(NewRequestedDate), nameof(NewRequestedTimeSlot), nameof(ModificationRequestNotes));
            string logDetailsMod = $"Attempting modification for Res ID: {ModificationReservationId}. NewClass: {NewRequestedClassId}, NewDate: {NewRequestedDate}, NewTime: {NewRequestedTimeSlot}, Notes: {!string.IsNullOrWhiteSpace(ModificationRequestNotes)}";
            await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification Attempt", "Processing", logDetailsMod);

            bool isModificationFormValid = true;
            if (ModificationReservationId <= 0)
            { ModelState.AddModelError(nameof(ModificationReservationId), "A valid reservation must be selected."); isModificationFormValid = false; } // Çevrildi
            if (!NewRequestedClassId.HasValue && !NewRequestedDate.HasValue && string.IsNullOrEmpty(NewRequestedTimeSlot) && string.IsNullOrWhiteSpace(ModificationRequestNotes))
            { ModelState.AddModelError("", "You must select at least one new field (class, date, time) or enter a note to request a modification."); isModificationFormValid = false; }
            if (ModificationRequestNotes != null && (ModificationRequestNotes.Length < 5 || ModificationRequestNotes.Length > 500) && !string.IsNullOrWhiteSpace(ModificationRequestNotes))
            { ModelState.AddModelError(nameof(ModificationRequestNotes), "Additional notes (if provided) must be between 5 and 500 characters."); isModificationFormValid = false; } // Çevrildi

            if (!isModificationFormValid || !ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Message"] = "Please correct the errors in the modification request form:<br/>" + string.Join("<br/>", errors.Any() ? errors : new List<string> { "An error occurred in the form." });
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification Attempt", "Failure - Invalid Model", $"Errors: {string.Join("; ", errors)}. Details: {logDetailsMod}");
                return Page();
            }

            try
            {
                var reservationToModify = await _context.Reservations.Include(r => r.Class).Include(r => r.User).Include(r => r.Term)
                                                .FirstOrDefaultAsync(r => r.Id == ModificationReservationId);

                if (reservationToModify == null)
                {
                    TempData["Message"] = "🚫 Reservation for modification not found."; TempData["MessageType"] = "danger";
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification", "Failure - Not Found", $"Res ID {ModificationReservationId} not found. Details: {logDetailsMod}");
                    return RedirectToPage();
                }
                if (reservationToModify.InstructorId != uid)
                {
                    TempData["Message"] = "🚫 You are not authorized to request a modification for this reservation."; TempData["MessageType"] = "danger";
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification", "Failure - Unauthorized", $"User {uid} not authorized for Res ID {ModificationReservationId}. Owner: {reservationToModify.InstructorId}. Details: {logDetailsMod}");
                    return RedirectToPage();
                }

                var termOfReservation = reservationToModify.Term;
                bool canOperate = false;
                if (termOfReservation != null) { canOperate = (reservationToModify.Status == "Pending" || reservationToModify.Status == "Approved"); }

                if (canOperate)
                {
                    StringBuilder detailsBuilder = new StringBuilder();
                    detailsBuilder.AppendLine($"Current Reservation: Class='{reservationToModify.Class?.Name}', Date='{reservationToModify.Date:dd.MM.yyyy}', Time='{reservationToModify.TimeSlot}'.");
                    detailsBuilder.AppendLine("--- Requested Changes ---");

                    string requestedClassNameForEmail = NewRequestedClassId.HasValue ? (await _context.Classes.FindAsync(NewRequestedClassId.Value))?.Name ?? "Unknown" : reservationToModify.Class?.Name ?? "N/A";
                    string requestedDateForEmail = NewRequestedDate.HasValue ? NewRequestedDate.Value.ToString("dd.MM.yyyy") : reservationToModify.Date.ToString("dd.MM.yyyy");
                    string requestedTimeSlotForEmail = !string.IsNullOrEmpty(NewRequestedTimeSlot) ? NewRequestedTimeSlot : reservationToModify.TimeSlot;
                    bool changeRequestedInFields = false;

                    if (NewRequestedClassId.HasValue && NewRequestedClassId.Value != reservationToModify.ClassId)
                    { detailsBuilder.AppendLine($"- New Class: {requestedClassNameForEmail} (ID: {NewRequestedClassId.Value})"); changeRequestedInFields = true; }
                    if (NewRequestedDate.HasValue && NewRequestedDate.Value.Date != reservationToModify.Date.Date)
                    {
                        if (termOfReservation != null && (NewRequestedDate.Value.Date < termOfReservation.StartDate || NewRequestedDate.Value.Date > termOfReservation.EndDate))
                        {
                            TempData["Message"] = $"⚠️ The requested new date ({NewRequestedDate.Value:dd.MM.yyyy}) is outside the current academic term of the reservation ({termOfReservation.Name}: {termOfReservation.StartDate:dd.MM.yyyy} - {termOfReservation.EndDate:dd.MM.yyyy}).";
                            TempData["MessageType"] = "warning";
                            await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification", "Failure - Date Out of Term", $"New date {NewRequestedDate.Value:dd.MM.yyyy} out of term for Res ID {ModificationReservationId}. Details: {logDetailsMod}");
                            return RedirectToPage();
                        }
                        detailsBuilder.AppendLine($"- New Date: {requestedDateForEmail}"); changeRequestedInFields = true;
                    }
                    if (!string.IsNullOrEmpty(NewRequestedTimeSlot) && NewRequestedTimeSlot != reservationToModify.TimeSlot)
                    { detailsBuilder.AppendLine($"- New Time Slot: {requestedTimeSlotForEmail}"); changeRequestedInFields = true; }

                    if (!string.IsNullOrWhiteSpace(ModificationRequestNotes)) { detailsBuilder.AppendLine($"Additional Notes: {ModificationRequestNotes}"); }
                    else if (!changeRequestedInFields)
                    {
                        TempData["Message"] = "ℹ️ You must select at least one new field or enter a note to request a modification."; TempData["MessageType"] = "info";
                        await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification", "Failure - No Changes Specified", $"No new fields or notes for Res ID {ModificationReservationId}. Details: {logDetailsMod}");
                        return RedirectToPage();
                    }

                    reservationToModify.PreviousStatus = reservationToModify.Status;
                    reservationToModify.Status = "ModificationRequested";
                    reservationToModify.ModificationRequestDetails = detailsBuilder.ToString();

                    reservationToModify.RequestedNewClassId = this.NewRequestedClassId;
                    reservationToModify.RequestedNewDate = this.NewRequestedDate;
                    reservationToModify.RequestedNewTimeSlot = this.NewRequestedTimeSlot;

                    await _context.SaveChangesAsync();
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification", "Success", $"Mod. requested for Res ID: {ModificationReservationId}. PrevStatus: {reservationToModify.PreviousStatus}. Details: {detailsBuilder.ToString()}");
                    TempData["Message"] = $" Your modification request for reservation (ID: {ModificationReservationId}) has been successfully sent. Awaiting admin approval."; // Çevrildi
                    TempData["MessageType"] = "success";

                    var adminUsers = await _context.Users.Where(u => u.Role.ToLower() == "admin").ToListAsync();
                    foreach (var admin in adminUsers)
                    {
                        if (!string.IsNullOrEmpty(admin.Email))
                        {
                            try
                            {
                                string emailNewValuesSection = "";
                                if (NewRequestedClassId.HasValue && NewRequestedClassId.Value != reservationToModify.ClassId) emailNewValuesSection += $"- Requested New Class: {requestedClassNameForEmail}<br/>";
                                if (NewRequestedDate.HasValue && NewRequestedDate.Value.Date != reservationToModify.Date.Date) emailNewValuesSection += $"- Requested New Date: {requestedDateForEmail}<br/>";
                                if (!string.IsNullOrEmpty(NewRequestedTimeSlot) && NewRequestedTimeSlot != reservationToModify.TimeSlot) emailNewValuesSection += $"- Requested New Time Slot: {requestedTimeSlotForEmail}<br/>";

                                if (string.IsNullOrEmpty(emailNewValuesSection) && !string.IsNullOrWhiteSpace(ModificationRequestNotes)) emailNewValuesSection = "Only additional notes were provided.<br/>";
                                else if (string.IsNullOrEmpty(emailNewValuesSection)) emailNewValuesSection = "No specific field changes requested, only notes provided or it's a general request.<br/>";

                                await _emailService.SendEmailAsync(admin.Email, $"Reservation Modification Request: ID {ModificationReservationId}", // Çevrildi
                                    $"Dear Admin,<br/><br/>Instructor <b>{UserName}</b> (ID: {uid}) has requested modifications for the reservation (ID: {ModificationReservationId}):<br/>" + // Çevrildi
                                    $"Current Reservation: Class='{reservationToModify.Class?.Name}', Date='{reservationToModify.Date:dd.MM.yyyy}', Time='{reservationToModify.TimeSlot}'<br/><br/>" + // Çevrildi
                                    $"<b>Requested New Information:</b><br/>{emailNewValuesSection}" + // Çevrildi
                                    $"<b>Additional Notes/Reason:</b><br/>{ModificationRequestNotes?.ReplaceLineEndings("<br/>") ?? "N/A"}<br/><br/>Please review the request in the admin panel.<br/><br/>System.");
                                await _loggingService.LogActionAsync(instructorIdentifier, "Modification Request Email Sent to Admin", "Success", $"To: {admin.Email} for Res ID {ModificationReservationId}");
                            }
                            catch (Exception emailEx)
                            {
                                await _loggingService.LogErrorAsync(emailEx, instructorIdentifier, $"Failed to send modification request email to admin {admin.Email} for Res ID {ModificationReservationId}");
                            }
                        }
                    }
                }
                else
                {
                    TempData["Message"] = "Modification cannot be requested for this reservation (e.g., status is not eligible or it does not belong to a valid term)."; TempData["MessageType"] = "info";
                    await _loggingService.LogActionAsync(instructorIdentifier, "Request Modification", "Failure - Cannot Operate", $"ResId {ModificationReservationId} status ({reservationToModify.Status}) not eligible for modification request. Details: {logDetailsMod}");
                }
            }
            catch (DbUpdateException dbEx)
            {
                await _loggingService.LogErrorAsync(dbEx, instructorIdentifier, $"DB Error requesting modification for Res ID: {ModificationReservationId}. Details: {logDetailsMod}");
                TempData["Message"] = " A database error occurred while sending your modification request."; TempData["MessageType"] = "danger";
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"General error requesting modification for Res ID: {ModificationReservationId}. Details: {logDetailsMod}");
                TempData["Message"] = " An unexpected error occurred while sending your modification request."; TempData["MessageType"] = "danger";
            }
            return RedirectToPage();
        }

        private async Task<string?> CanCreateReservationInstance(int instructorUid, int classId, DateTime date, string timeSlot, string instructorIdentifierForLog, Term? termContext)
        {
            if (termContext == null)
            {
                await _loggingService.LogActionAsync(instructorIdentifierForLog, "Check Reservation Instance", "Failure - No Term Context", $"Date: {date:dd.MM.yyyy}");
                return $"No valid academic term found for the reservation date.";
            }
            if (date.Date < termContext.StartDate.Date || date.Date > termContext.EndDate.Date)
            {
                await _loggingService.LogActionAsync(instructorIdentifierForLog, "Check Reservation Instance", "Failure - Date Out of Term", $"Attempt to reserve on {date:dd.MM.yyyy}, which is outside term {termContext.Name} ({termContext.StartDate:dd.MM.yyyy}-{termContext.EndDate:dd.MM.yyyy})");
                return $"The selected date ({date:dd.MM.yyyy}) is outside the current academic term ({termContext.Name}: {termContext.StartDate:dd.MM.yyyy} - {termContext.EndDate:dd.MM.yyyy}).";
            }
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                await _loggingService.LogActionAsync(instructorIdentifierForLog, "Check Reservation Instance", "Failure - Weekend", $"Attempt to reserve on weekend: {date:dd.MM.yyyy}");
                return $"Reservations cannot be made on weekends ({date:dd.MM.yyyy}).";
            }

            var holidays = await _calendarService.GetHolidaysAsync(new DateTime(date.Year, 1, 1), new DateTime(date.Year, 12, 31));
            var holidayInfo = holidays.FirstOrDefault(h => (DateTime.TryParse(h.Start?.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly) && parsedDateOnly.Date == date.Date) || (h.Start?.DateTimeDateTimeOffset.HasValue == true && h.Start.DateTimeDateTimeOffset.Value.Date == date.Date));
            if (holidayInfo != null)
            {
                await _loggingService.LogActionAsync(instructorIdentifierForLog, "Check Reservation Instance", "Failure - Holiday Blocked", $"Instructor reservation attempt BLOCKED for holiday: {holidayInfo.Summary} - {date:dd.MM.yyyy}.");
                return $"🗓️ Cannot make a reservation on a public holiday ({holidayInfo.Summary ?? "Unspecified"} - {date:dd.MM.yyyy}).";
            }

            bool instructorSlotApproved = await _context.Reservations.AnyAsync(r => r.InstructorId == instructorUid && r.Date.Date == date.Date && r.TimeSlot == timeSlot && r.Status == "Approved");
            if (instructorSlotApproved)
            {
                await _loggingService.LogActionAsync(instructorIdentifierForLog, "Check Reservation Instance", "Failure - Instructor Conflict (Approved)", $"Instructor already has an approved class at {date:dd.MM.yyyy} {timeSlot}");
                return $"❗ You already have an approved class at this date and time ({date:dd.MM.yyyy} {timeSlot}).";
            }

            bool classSlotApprovedOther = await _context.Reservations.AnyAsync(r => r.ClassId == classId && r.Date.Date == date.Date && r.TimeSlot == timeSlot && r.Status == "Approved" && r.InstructorId != instructorUid);
            if (classSlotApprovedOther)
            {
                var conflictingClass = await _context.Classes.FindAsync(classId);
                await _loggingService.LogActionAsync(instructorIdentifierForLog, "Check Reservation Instance", "Failure - Class Conflict (Approved)", $"Class '{conflictingClass?.Name}' is already booked by another instructor at {date:dd.MM.yyyy} {timeSlot}");
                return $"❗ This classroom ({conflictingClass?.Name ?? "Unknown Class"}) and time slot ({date:dd.MM.yyyy} {timeSlot}) is already approved for another instructor."; // Çevrildi
            }

            return null;
        }

        private void ClearModelStateForOtherForms(params string[] keepPrefixes)
        {
            var keysToClear = ModelState.Keys.Where(key => !keepPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || key.Equals(prefix, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var key in keysToClear) { ModelState.Remove(key); }
        }

        public async Task<JsonResult> OnGetMonthlyReservations(string start, string end)
        {
            var instructorIdentifier = GetInstructorIdentifier();
            var userIdString = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int uid))
            {
                await _loggingService.LogActionAsync(instructorIdentifier, "Get Monthly Reservations (Calendar)", "Failure - Unauthorized/Invalid Session", "User ID not found in session or invalid.");
                return new JsonResult(new { error = "Invalid session or user." }) { StatusCode = 401 };
            }

            await _loggingService.LogActionAsync(instructorIdentifier, "Get Monthly Reservations (Calendar)", "Attempt", $"Range: {start} to {end}, UID: {uid}");

            if (!DateTime.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime startDate) ||
                !DateTime.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime endDate))
            {
                await _loggingService.LogActionAsync(instructorIdentifier, "Get Monthly Reservations (Calendar)", "Failure - Invalid Date Format", $"Start: {start}, End: {end}");
                return new JsonResult(new { error = "Invalid date format received from calendar." }) { StatusCode = 400 };
            }

            try
            {
                var filteredReservations = await _context.Reservations
                    .Include(r => r.Class)
                    .Where(r => r.InstructorId == uid && r.Date >= startDate.Date && r.Date < endDate.Date)
                    .ToListAsync();

                var events = new List<object>();
                foreach (var r in filteredReservations)
                {
                    string eventTitle = $"{(r.Class?.Name ?? "N/A")} - {r.Status}";
                    string eventColor = r.Status switch
                    {
                        "Approved" => "green",
                        "Rejected" => "red",
                        "Conflict" => "darkred",
                        "Pending" => "orange",
                        "Cancellation Requested" => "#0dcaf0",
                        "ModificationRequested" => "#6f42c1",
                        "Cancelled" => "grey",
                        _ => "grey"
                    };

                    DateTime eventStartDateTime = r.Date;
                    DateTime eventEndDateTime = r.Date.AddHours(1);
                    bool isAllDayEvent = true;

                    if (!string.IsNullOrEmpty(r.TimeSlot))
                    {
                        var timeParts = r.TimeSlot.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        if (timeParts.Length == 2)
                        {
                            if (TimeSpan.TryParse(timeParts[0].Trim(), CultureInfo.InvariantCulture, out TimeSpan parsedStartTime) &&
                                TimeSpan.TryParse(timeParts[1].Trim(), CultureInfo.InvariantCulture, out TimeSpan parsedEndTime))
                            {
                                eventStartDateTime = r.Date.Add(parsedStartTime);
                                eventEndDateTime = r.Date.Add(parsedEndTime);
                                isAllDayEvent = false;
                            }
                        }
                    }

                    events.Add(new
                    {
                        id = r.Id,
                        title = eventTitle,
                        start = eventStartDateTime.ToString("o"),
                        end = eventEndDateTime.ToString("o"),
                        color = eventColor,
                        allDay = isAllDayEvent,
                        extendedProps = new
                        {
                            status = r.Status,
                            className = r.Class?.Name ?? "N/A",
                            timeSlot = r.TimeSlot ?? "N/A",
                            description = $"Class: {r.Class?.Name ?? "N/A"}\nTime: {r.TimeSlot ?? "N/A"}\nStatus: {r.Status}"
                        }
                    });
                }
                await _loggingService.LogActionAsync(instructorIdentifier, "Get Monthly Reservations (Calendar)", "Success", $"Found {events.Count} events for UID: {uid}.");
                return new JsonResult(events);
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Error fetching monthly reservations for calendar. UID: {uid}, Range: {start} to {end}");
                return new JsonResult(new { error = "Server error while fetching calendar data." }) { StatusCode = 500 };
            }
        }
    }
}