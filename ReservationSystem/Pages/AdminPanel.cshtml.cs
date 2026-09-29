using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models;
using ReservationSystem.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

//AI PROMPT: "Build a full-featured admin panel backend using ASP.NET Razor Pages to control reservations. The system should include auto conflict detection, term control, Google Calendar holiday blocking, modification logic, logging, and email integration."

//AI PROMPT:"Write an ASP.NET Core Razor Page backend for an admin to manage classroom reservations, check Google Calendar public holidays, auto-reject on holidays, and notify users via email. Include all CRUD operations, logging, and session-based admin tracking.

//AI PROMPT:"Implement a Razor Page admin backend for managing reservations with export to JSON, public holiday checking, event calendar support, and role-based authorization. Include logging actions and error handling."


namespace ReservationSystem.Pages
{
    [Authorize(Roles = "admin")]
    public class AdminPanelModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleCalendarService _calendarService;
        private readonly LoggingService _loggingService;
        private readonly EmailService _emailService;

        public AdminPanelModel(ApplicationDbContext context, GoogleCalendarService calendarService, LoggingService loggingService, EmailService emailService)
        {
            _context = context;
            _calendarService = calendarService;
            _loggingService = loggingService;
            _emailService = emailService;
        }

        public List<Reservation> Reservations { get; set; } = new();
        public Dictionary<DateTime, string> PublicHolidays { get; set; } = new();
        public List<BlockedHolidayAttempt> BlockedHolidayAttemptsLog { get; set; } = new();

        [TempData]
        public string? Message { get; set; }
        [TempData]
        public string? MessageType { get; set; }

        private string GetAdminIdentifier()
        {
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? "UnknownAdmin";
        }

        private async Task EnsurePublicHolidaysLoadedAsync(int year)
        {
            var adminId = GetAdminIdentifier();
            try
            {
                if (!PublicHolidays.Any(kvp => kvp.Key.Year == year))
                {
                    var holidaysFromService = await _calendarService.GetHolidaysAsync(new DateTime(year, 1, 1), new DateTime(year, 12, 31));
                    foreach (var holidayEvent in holidaysFromService)
                    {
                        DateTime? holidayDateKey = null;
                        if (DateTime.TryParse(holidayEvent.Start?.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                            holidayDateKey = parsedDate.Date;
                        else if (holidayEvent.Start?.DateTimeDateTimeOffset.HasValue == true)
                            holidayDateKey = holidayEvent.Start.DateTimeDateTimeOffset.Value.Date;

                        if (holidayDateKey.HasValue && !PublicHolidays.ContainsKey(holidayDateKey.Value))
                        {
                            PublicHolidays[holidayDateKey.Value] = holidayEvent.Summary ?? "Public Holiday"; // Çevrildi
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error in EnsurePublicHolidaysLoadedAsync for year {year}.");
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "View Admin Panel", "Attempt");

            try
            {
                Reservations = await _context.Reservations
                    .Include(r => r.User).Include(r => r.Instructor).Include(r => r.Class)
                    .OrderByDescending(r => r.Date)
                    .ThenBy(r => r.TimeSlot)
                    .ThenBy(r => (r.Status == "Pending" || r.Status == "ModificationRequested" || r.Status == "Cancellation Requested") ? 0 : (r.Status == "Approved" ? 1 : (r.Status == "Conflict" ? 2 : (r.Status == "Rejected" ? 3 : 4))))
                    .ToListAsync();

                BlockedHolidayAttemptsLog = await _context.BlockedHolidayAttempts
                    .Include(bha => bha.Instructor)
                    .Include(bha => bha.Class)
                    .OrderByDescending(bha => bha.AttemptTimestamp)
                    .Take(50)
                    .ToListAsync();

                await _loggingService.LogActionAsync(adminId, "View Admin Panel", "Success", $"Loaded {Reservations.Count} reservations and {BlockedHolidayAttemptsLog.Count} holiday attempts.");

                var yearsInReservations = Reservations.Select(r => r.Date.Year)
                                            .Concat(BlockedHolidayAttemptsLog.Select(bha => bha.RequestedDate.Year))
                                            .Distinct().ToList();
                if (!yearsInReservations.Any()) { yearsInReservations.Add(DateTime.Now.Year); }

                await _loggingService.LogActionAsync(adminId, "Load Public Holidays for Admin Panel", "Attempt", $"Years to check: {string.Join(", ", yearsInReservations)}");
                foreach (var year in yearsInReservations) { await EnsurePublicHolidaysLoadedAsync(year); }
                if (!PublicHolidays.Any() && !yearsInReservations.Contains(DateTime.Now.Year))
                { await EnsurePublicHolidaysLoadedAsync(DateTime.Now.Year); }
                await _loggingService.LogActionAsync(adminId, "Load Public Holidays for Admin Panel", "Success", $"Total public holidays loaded: {PublicHolidays.Count}");

                bool changesMadeToReservations = false;
                List<string> autoRejectedInfos = new List<string>();
                var pendingReservations = Reservations.Where(r => r.Status == "Pending").ToList();

                foreach (var res in pendingReservations)
                {
                    if (PublicHolidays.TryGetValue(res.Date.Date, out var holidayName))
                    {
                        var originalReservationInList = Reservations.FirstOrDefault(r => r.Id == res.Id);
                        if (originalReservationInList != null)
                        {
                            originalReservationInList.PreviousStatus = originalReservationInList.Status;
                            originalReservationInList.Status = "Rejected";
                            originalReservationInList.ModificationRequestDetails = $"Automatically rejected by the system due to public holiday ({holidayName})."; // Çevrildi

                            changesMadeToReservations = true;
                            autoRejectedInfos.Add($"ID {res.Id} ({res.User?.FullName}) {res.Date:dd.MM.yyyy} - {holidayName}");
                            await _loggingService.LogActionAsync(adminId, "Auto-Reject Reservation (Holiday)", "Success", $"Reservation ID {res.Id} (User: {res.User?.Email}) auto-rejected due to public holiday: {holidayName}");

                            if (res.User != null && !string.IsNullOrEmpty(res.User.Email))
                            {
                                string emailSubject = "Your Reservation Request and Public Holiday Information ❌"; // Çevrildi
                                string classNameForEmail = res.Class?.Name ?? "Unspecified Class"; // Çevrildi
                                string emailBody = $"Dear {res.User.FullName},<br/><br/>" + // Çevrildi
                                                 $"Your reservation request (ID: {res.Id}) for the classroom <b>{classNameForEmail}</b> " + // Çevrildi
                                                 $"on {res.Date:dd.MM.yyyy} at {res.TimeSlot} " + // Çevrildi
                                                 $"has been automatically <span style='color:red; font-weight:bold;'>rejected</span> by the system because it coincides with the <b>{holidayName}</b> public holiday.<br/><br/>" + // Çevrildi
                                                 $"Thank you for your understanding.<br/>Reservation System"; 
                                try
                                {
                                    await _emailService.SendEmailAsync(res.User.Email, emailSubject, emailBody);
                                    await _loggingService.LogActionAsync(adminId, "Auto-Reject Email Sent (Holiday)", "Success", $"To: {res.User.Email} for Res ID {res.Id}");
                                }
                                catch (Exception ex)
                                {
                                    await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send auto-reject (holiday) email to {res.User.Email} for Res ID {res.Id}.");
                                }
                            }
                        }
                    }
                }

                if (changesMadeToReservations)
                {
                    await _context.SaveChangesAsync();
                    if (autoRejectedInfos.Any())
                    {
                        TempData["Message"] = $"The following 'Pending' reservations were automatically rejected due to public holidays, and instructors have been notified:<br/>- {string.Join("<br/>- ", autoRejectedInfos)}"; 
                        TempData["MessageType"] = "info";
                    }
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, "Error loading Admin Panel data.");
                Message = "An error occurred while loading the admin panel.";
                MessageType = "danger";
            }
            return Page();
        }

        public async Task<JsonResult> OnGetCalendarReservationsAsync(string start, string end)
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "Get Calendar Reservations", "Attempt", $"Range: {start} to {end}");

            if (!DateTime.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime startDate) ||
                !DateTime.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime endDate))
            {
                await _loggingService.LogActionAsync(adminId, "Get Calendar Reservations", "Failure - Invalid Date Format", $"Start: {start}, End: {end}");
                return new JsonResult(new { error = "Invalid date format." }) { StatusCode = 400 };
            }

            try
            {
                await _loggingService.LogActionAsync(adminId, "Load Public Holidays for Calendar", "Attempt", $"Years: {startDate.Year}-{endDate.Year}");
                for (int year = startDate.Year; year <= endDate.Year; year++) { await EnsurePublicHolidaysLoadedAsync(year); }
                await _loggingService.LogActionAsync(adminId, "Load Public Holidays for Calendar", "Success");

                var reservations = await _context.Reservations
                    .Include(r => r.Class)
                    .Include(r => r.Instructor)
                    .Include(r => r.User)
                    .Where(r => r.Date >= startDate.Date && r.Date < endDate.Date)
                    .ToListAsync();

                var events = new List<object>();
                foreach (var r in reservations)
                {
                    string className = r.Class?.Name ?? "N/A";
                    string timeSlotString = r.TimeSlot ?? "";
                    string instructorName = r.Instructor?.FullName ?? "N/A";
                    string requestedBy = r.User?.FullName ?? "N/A";

                    string eventTitle = $"{className} ({timeSlotString})\nInstructor: {instructorName}\nRequested By: {requestedBy}\n[{r.Status}]"; 
                    if (r.Status == "ModificationRequested" && !string.IsNullOrEmpty(r.ModificationRequestDetails) && !(r.ModificationRequestDetails?.Contains("automatically rejected") ?? false)) 
                    {
                        eventTitle += $"\nMod. Details: {r.ModificationRequestDetails.Substring(0, Math.Min(r.ModificationRequestDetails.Length, 50))}{(r.ModificationRequestDetails.Length > 50 ? "..." : "")}"; 
                    }
                    else if (r.Status == "Rejected" && (r.ModificationRequestDetails?.Contains("automatically rejected") ?? false)) 
                    {
                        eventTitle += $"\nReason: {r.ModificationRequestDetails}"; 
                    }

                    string color = r.Status switch
                    {
                        "Approved" => "green",
                        "Rejected" => "red",
                        "Pending" => "orange",
                        "Conflict" => "darkred",
                        "Cancellation Requested" => "#0dcaf0",
                        "ModificationRequested" => "#6f42c1",
                        "Cancelled" => "grey",
                        _ => "grey"
                    };

                    PublicHolidays.TryGetValue(r.Date.Date, out var holidayName);

                    DateTime eventStartDateTime = r.Date;
                    DateTime eventEndDateTime = r.Date.AddHours(1);
                    bool isAllDay = true;

                    if (!string.IsNullOrEmpty(timeSlotString))
                    {
                        var timeParts = timeSlotString.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        if (timeParts.Length == 2)
                        {
                            if (TimeSpan.TryParse(timeParts[0].Trim(), CultureInfo.InvariantCulture, out TimeSpan parsedStartTime) &&
                                TimeSpan.TryParse(timeParts[1].Trim(), CultureInfo.InvariantCulture, out TimeSpan parsedEndTime))
                            {
                                eventStartDateTime = r.Date.Add(parsedStartTime);
                                eventEndDateTime = r.Date.Add(parsedEndTime);
                                isAllDay = false;
                            }
                        }
                    }

                    events.Add(new
                    {
                        id = r.Id,
                        title = eventTitle,
                        start = eventStartDateTime.ToString("o"),
                        end = eventEndDateTime.ToString("o"),
                        allDay = isAllDay,
                        color = color,
                        extendedProps = new
                        {
                            status = r.Status,
                            className = className,
                            timeSlot = timeSlotString,
                            instructor = instructorName,
                            requestedBy = requestedBy,
                            modificationDetails = r.ModificationRequestDetails,
                            isHoliday = PublicHolidays.ContainsKey(r.Date.Date),
                            holidayName = holidayName,
                            description = $"Class: {className}\nTime: {timeSlotString}\nInstructor: {instructorName}\nStatus: {r.Status}" + (PublicHolidays.ContainsKey(r.Date.Date) ? $"\nHoliday: {holidayName}" : "") 
                        }
                    });
                }
                await _loggingService.LogActionAsync(adminId, "Get Calendar Reservations", "Success", $"Found {events.Count} events for calendar.");
                return new JsonResult(events);
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, "Error fetching calendar reservations.");
                return new JsonResult(new { error = "Server error fetching calendar data." }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "Approve Reservation Attempt", "Processing", $"Attempting to approve Reservation ID: {id}");

            Reservation? reservationToProcess = null;
            try
            {
                reservationToProcess = await _context.Reservations
                    .Include(r => r.User).Include(r => r.Instructor).Include(r => r.Class).Include(r => r.Term)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (reservationToProcess == null)
                {
                    TempData["Message"] = "Reservation not found."; MessageType = "danger"; 
                    await _loggingService.LogActionAsync(adminId, "Approve Reservation", "Failure - Not Found", $"Reservation ID {id} not found.");
                    return RedirectToPage();
                }

                string logDetails = $"ResId: {id}, CurrentStatus: {reservationToProcess.Status}, User: {reservationToProcess.User?.Email}";

                if (reservationToProcess.Status == "Rejected" && (reservationToProcess.ModificationRequestDetails?.Contains("automatically rejected") ?? false)) 
                {
                    TempData["Message"] = $"This reservation (ID: {id}) has already been rejected by the system due to a public holiday and cannot be processed again."; MessageType = "info"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Approve Reservation", "Failure - Already Auto-Rejected", logDetails);
                    return RedirectToPage();
                }

                string originalStatusOfRequest = reservationToProcess.Status;
                if (originalStatusOfRequest != "Pending" && originalStatusOfRequest != "ModificationRequested")
                {
                    TempData["Message"] = $"This reservation (ID: {id}) is not in a state that can be approved (Current Status: {originalStatusOfRequest})."; MessageType = "info"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Approve Reservation", "Failure - Invalid Status for Approval", logDetails);
                    return RedirectToPage();
                }

                if (originalStatusOfRequest == "ModificationRequested")
                {
                    if (!reservationToProcess.RequestedNewDate.HasValue || !reservationToProcess.RequestedNewClassId.HasValue || string.IsNullOrEmpty(reservationToProcess.RequestedNewTimeSlot))
                    {
                        TempData["Message"] = "Modification request could not be approved: Requested new date, class, or time slot information is missing. The reservation has been reverted to its previous state."; // Çevrildi
                        MessageType = "danger";
                        reservationToProcess.Status = reservationToProcess.PreviousStatus ?? "Pending";
                        reservationToProcess.RequestedNewClassId = null; reservationToProcess.RequestedNewDate = null; reservationToProcess.RequestedNewTimeSlot = null;
                        await _context.SaveChangesAsync();
                        await _loggingService.LogActionAsync(adminId, "Approve Modification", "Failure - Missing Modification Details", $"ResId: {id}. Reverted to {reservationToProcess.Status}.");
                        return RedirectToPage();
                    }

                    await EnsurePublicHolidaysLoadedAsync(reservationToProcess.RequestedNewDate.Value.Year);
                    bool isNewDateHoliday = PublicHolidays.TryGetValue(reservationToProcess.RequestedNewDate.Value.Date, out var newHolidayName);

                    bool newSlotClassConflict = await _context.Reservations.AnyAsync(r => r.Id != reservationToProcess.Id && r.ClassId == reservationToProcess.RequestedNewClassId.Value && r.Date == reservationToProcess.RequestedNewDate.Value && r.TimeSlot == reservationToProcess.RequestedNewTimeSlot && r.Status == "Approved");
                    if (newSlotClassConflict)
                    {
                        TempData["Message"] = $"Modification request could not be approved. Another approved reservation already exists for the requested new classroom/time ({reservationToProcess.RequestedNewDate.Value:dd.MM.yyyy} {reservationToProcess.RequestedNewTimeSlot}). The original request has been reverted to its previous state."; // Çevrildi
                        reservationToProcess.Status = reservationToProcess.PreviousStatus ?? "Pending";
                        await _context.SaveChangesAsync(); MessageType = "danger";
                        await _loggingService.LogActionAsync(adminId, "Approve Modification", "Failure - New Slot Class Conflict", $"ResId: {id}. Details: NewClassId={reservationToProcess.RequestedNewClassId}, NewDate={reservationToProcess.RequestedNewDate}, NewTimeSlot={reservationToProcess.RequestedNewTimeSlot}. Reverted to {reservationToProcess.Status}.");
                        return RedirectToPage();
                    }

                    bool newSlotInstructorConflict = await _context.Reservations.AnyAsync(r => r.Id != reservationToProcess.Id && r.InstructorId == reservationToProcess.InstructorId && r.Date == reservationToProcess.RequestedNewDate.Value && r.TimeSlot == reservationToProcess.RequestedNewTimeSlot && r.Status == "Approved");
                    if (newSlotInstructorConflict)
                    {
                        TempData["Message"] = $" Modification request could not be approved. The instructor already has another approved reservation in the requested new time slot ({reservationToProcess.RequestedNewDate.Value:dd.MM.yyyy} {reservationToProcess.RequestedNewTimeSlot}). The original request has been reverted to its previous state."; 
                        reservationToProcess.Status = reservationToProcess.PreviousStatus ?? "Pending";
                        await _context.SaveChangesAsync(); MessageType = "danger";
                        await _loggingService.LogActionAsync(adminId, "Approve Modification", "Failure - New Slot Instructor Conflict", $"ResId: {id}. Details: InstructorId={reservationToProcess.InstructorId}, NewDate={reservationToProcess.RequestedNewDate}, NewTimeSlot={reservationToProcess.RequestedNewTimeSlot}. Reverted to {reservationToProcess.Status}.");
                        return RedirectToPage();
                    }

                    var newTermForNewDate = await _context.Terms.FirstOrDefaultAsync(t => reservationToProcess.RequestedNewDate.Value >= t.StartDate && reservationToProcess.RequestedNewDate.Value <= t.EndDate && t.IsActive);
                    if (newTermForNewDate == null)
                    {
                        TempData["Message"] = $" Modification request could not be approved. The requested new date ({reservationToProcess.RequestedNewDate.Value:dd.MM.yyyy}) does not belong to a valid active academic term. The original request has been reverted to its previous state."; 
                        reservationToProcess.Status = reservationToProcess.PreviousStatus ?? "Pending";
                        await _context.SaveChangesAsync(); MessageType = "danger";
                        await _loggingService.LogActionAsync(adminId, "Approve Modification", "Failure - New Date Not In Active Term", $"ResId: {id}. NewDate={reservationToProcess.RequestedNewDate}. Reverted to {reservationToProcess.Status}.");
                        return RedirectToPage();
                    }

                    var newApprovedReservation = new Reservation
                    {
                        ClassId = reservationToProcess.RequestedNewClassId.Value,
                        Date = reservationToProcess.RequestedNewDate.Value,
                        TimeSlot = reservationToProcess.RequestedNewTimeSlot,
                        Status = "Approved",
                        TermId = newTermForNewDate.Id,
                        UserId = reservationToProcess.UserId,
                        InstructorId = reservationToProcess.InstructorId,
                    };
                    _context.Reservations.Add(newApprovedReservation);

                    string oldReservationOriginalStatus = reservationToProcess.PreviousStatus ?? reservationToProcess.Status;
                    reservationToProcess.Status = "Cancelled";
                    reservationToProcess.ModificationRequestDetails = $"Modification request approved. New reservation ID will be: {newApprovedReservation.Id} (This ID is not yet assigned, will be after SaveChangesAsync)"; 

                    await _loggingService.LogActionAsync(adminId, "Approve Modification - Old Cancelled", "Processing", $"Old ResId={reservationToProcess.Id} status to be Cancelled. Original status was {oldReservationOriginalStatus}.");

                    await _context.SaveChangesAsync();
                    newApprovedReservation.ModificationRequestDetails = $"This reservation is the approved modification of reservation ID {reservationToProcess.Id}."; 
                    reservationToProcess.ModificationRequestDetails = $"Modification request approved. New reservation ID: {newApprovedReservation.Id}"; 
                    await _context.SaveChangesAsync();

                    await _loggingService.LogActionAsync(adminId, "Approve Modification - New Created", "Success", $"New ResId={newApprovedReservation.Id} created and approved for original ResId={id}. Old ResId {id} Cancelled.");

                    List<string> autoConflictedInfosForNew = await AutoHandleConflicts(newApprovedReservation, adminId);
                    if (autoConflictedInfosForNew.Any()) { await _context.SaveChangesAsync(); }

                    if (reservationToProcess.User != null && !string.IsNullOrEmpty(reservationToProcess.User.Email))
                    {
                        string emailSubject = "Your Reservation Modification Request Has Been Approved and Applied "; 
                        string newClassName = (await _context.Classes.FindAsync(newApprovedReservation.ClassId))?.Name ?? "N/A";
                        string emailBody = $"Dear {reservationToProcess.User.FullName},<br/><br/>Your modification request for <b>{reservationToProcess.Class?.Name}</b> ({reservationToProcess.Date:dd.MM.yyyy} {reservationToProcess.TimeSlot}) has been approved.<br/>" + 
                                         $"<b>Old Reservation (ID: {reservationToProcess.Id}):</b> Status updated to '{reservationToProcess.Status}'.<br/>" + // Çevrildi
                                         $"<b>New Approved Reservation (ID: {newApprovedReservation.Id}):</b> Class: {newClassName}, Date: {newApprovedReservation.Date:dd.MM.yyyy}, Time: {newApprovedReservation.TimeSlot}.<br/>"; 
                        if (isNewDateHoliday) { emailBody += $"<br/><b>Please Note:</b> The new approved date {newApprovedReservation.Date:dd.MM.yyyy} is a public holiday: {newHolidayName}.<br/>"; } 
                        emailBody += "<br/>Thank you,<br/>Reservation System"; 
                        try
                        {
                            await _emailService.SendEmailAsync(reservationToProcess.User.Email, emailSubject, emailBody);
                            await _loggingService.LogActionAsync(adminId, "Modification Approval Email Sent", "Success", $"To: {reservationToProcess.User.Email} for New ResId {newApprovedReservation.Id}, Old ResId {id}");
                        }
                        catch (Exception ex) { await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send modification approval email to {reservationToProcess.User.Email} for New ResId {newApprovedReservation.Id}"); }
                    }
                    TempData["Message"] = $"Modification request (ID: {id}) approved. Old reservation set to '{reservationToProcess.Status}', new reservation (ID: {newApprovedReservation.Id}) created as 'Approved'."; 
                    if (autoConflictedInfosForNew.Any()) TempData["Message"] += $" {autoConflictedInfosForNew.Count} other request(s) for the new slot set to 'Conflict': {string.Join(", ", autoConflictedInfosForNew)}."; 
                    MessageType = "success";
                }
                else if (originalStatusOfRequest == "Pending")
                {
                    await EnsurePublicHolidaysLoadedAsync(reservationToProcess.Date.Year);
                    bool isApprovedDateHoliday = PublicHolidays.TryGetValue(reservationToProcess.Date.Date, out var approvedHolidayName);

                    reservationToProcess.Status = "Approved";
                    reservationToProcess.ModificationRequestDetails = null; reservationToProcess.PreviousStatus = null;
                    reservationToProcess.RequestedNewClassId = null; reservationToProcess.RequestedNewDate = null; reservationToProcess.RequestedNewTimeSlot = null;

                    List<string> autoConflictedInfos = await AutoHandleConflicts(reservationToProcess, adminId);

                    await _context.SaveChangesAsync();
                    await _loggingService.LogActionAsync(adminId, "Approve Pending Reservation", "Success", $"ResId={id} (User: {reservationToProcess.User?.Email}) approved. Holiday: {isApprovedDateHoliday}");

                    if (reservationToProcess.User != null && !string.IsNullOrEmpty(reservationToProcess.User.Email))
                    {
                        string emailSubject = "Your Reservation Has Been Approved "; 
                        string emailBody = $"Dear {reservationToProcess.User.FullName},<br/><br/>Your reservation request for classroom <b>{reservationToProcess.Class?.Name}</b> on {reservationToProcess.Date:dd.MM.yyyy} at {reservationToProcess.TimeSlot} has been <span style='color:green; font-weight:bold;'>approved</span>."; // Çevrildi
                        if (isApprovedDateHoliday) { emailBody += $"<br/><br/><b>Please Note:</b> The approved date {reservationToProcess.Date:dd.MM.yyyy} is a public holiday: {approvedHolidayName}."; } 
                        emailBody += "<br/><br/>Thank you,<br/>Reservation System"; 
                        try
                        {
                            await _emailService.SendEmailAsync(reservationToProcess.User.Email, emailSubject, emailBody);
                            await _loggingService.LogActionAsync(adminId, "Pending Approval Email Sent", "Success", $"To: {reservationToProcess.User.Email} for ResId {id}");
                        }
                        catch (Exception ex) { await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send pending approval email to {reservationToProcess.User.Email} for ResId {id}"); }
                    }
                    string finalMessage = $"Reservation (ID: {id}) successfully approved."; 
                    if (isApprovedDateHoliday) { finalMessage += $" (Warning: Public Holiday {approvedHolidayName})"; } 
                    if (autoConflictedInfos.Any()) { finalMessage += $" Additionally, due to this approval, {autoConflictedInfos.Count} other request(s) ({string.Join(", ", autoConflictedInfos)}) were marked as 'Conflict'."; } 
                    TempData["Message"] = finalMessage; MessageType = "success";
                }
                else
                {
                    TempData["Message"] = $"This reservation (ID: {id}) is not in a state that can be approved (Current Status: {originalStatusOfRequest})."; MessageType = "info"; 
                    await _loggingService.LogActionAsync(adminId, "Approve Reservation", "Failure - Invalid Status (Final Check)", logDetails);
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error during OnPostApproveAsync for Res ID {id}. Reservation Status: {reservationToProcess?.Status ?? "N/A"}");
                TempData["Message"] = "An unexpected error occurred while approving the reservation."; MessageType = "danger"; 
            }
            return RedirectToPage();
        }

        private async Task<List<string>> AutoHandleConflicts(Reservation approvedReservation, string adminId)
        {
            var conflictingReservations = await _context.Reservations
                .Where(r => r.Id != approvedReservation.Id &&
                             r.ClassId == approvedReservation.ClassId &&
                             r.Date == approvedReservation.Date &&
                             r.TimeSlot == approvedReservation.TimeSlot &&
                             (r.Status == "Pending" || r.Status == "ModificationRequested"))
                .Include(r => r.User).Include(r => r.Class)
                .ToListAsync();

            List<string> autoConflictedInfos = new List<string>();
            if (conflictingReservations.Any())
            {
                await _loggingService.LogActionAsync(adminId, "AutoHandleConflicts - Start", "Processing", $"Found {conflictingReservations.Count} potential conflicts for approved ResId {approvedReservation.Id}");
                foreach (var conflictRes in conflictingReservations)
                {
                    string conflictPreviousStatus = conflictRes.Status;
                    conflictRes.PreviousStatus = conflictRes.Status;
                    conflictRes.Status = "Conflict";
                    conflictRes.ModificationRequestDetails = $"This request was automatically set to 'Conflict' because another reservation (ID: {approvedReservation.Id}) for the same slot was approved."; // Çevrildi

                    await _loggingService.LogActionAsync(adminId, "Auto-Set to Conflict", "Success", $"ResId={conflictRes.Id} (User: {conflictRes.User?.Email}) auto-set to Conflict due to approved ResId={approvedReservation.Id}. Prev Status: {conflictPreviousStatus}");
                    autoConflictedInfos.Add($"ID {conflictRes.Id} (Requester: {conflictRes.User?.FullName ?? "N/A"})"); // Çevrildi

                    if (conflictRes.User != null && !string.IsNullOrEmpty(conflictRes.User.Email))
                    {
                        string conflictSubject = "Update on Your Reservation Request "; // Çevrildi
                        string classNameForEmail = conflictRes.Class?.Name ?? "Unspecified Class"; // Çevrildi
                        string conflictBody = $"Dear {conflictRes.User.FullName},<br/><br/>Your reservation request (ID: {conflictRes.Id}) for classroom <b>{classNameForEmail}</b> on {conflictRes.Date:dd.MM.yyyy} at {conflictRes.TimeSlot} has been automatically set to 'Conflict' because another reservation for the same time slot was approved.<br/><br/>Thank you for your understanding.<br/>Reservation System"; // Çevrildi
                        try
                        {
                            await _emailService.SendEmailAsync(conflictRes.User.Email, conflictSubject, conflictBody);
                            await _loggingService.LogActionAsync(adminId, "Auto-Conflict Email Sent", "Success", $"To: {conflictRes.User.Email} for ResId {conflictRes.Id}");
                        }
                        catch (Exception ex) { await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send auto-conflict email to {conflictRes.User.Email} for ResId={conflictRes.Id}."); }
                    }
                }
            }
            else
            {
                await _loggingService.LogActionAsync(adminId, "AutoHandleConflicts - End", "Info", $"No conflicts found for approved ResId {approvedReservation.Id}");
            }
            return autoConflictedInfos;
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "Reject Reservation Attempt", "Processing", $"Attempting to reject Reservation ID: {id}");
            Reservation? reservationToReject = null;

            try
            {
                reservationToReject = await _context.Reservations.Include(r => r.User).Include(r => r.Instructor).Include(r => r.Class).FirstOrDefaultAsync(r => r.Id == id);
                if (reservationToReject == null)
                {
                    TempData["Message"] = "Reservation not found."; MessageType = "danger"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Reject Reservation", "Failure - Not Found", $"Reservation ID {id} not found.");
                    return RedirectToPage();
                }

                string statusBeforeRejection = reservationToReject.Status;
                string logDetails = $"ResId: {id}, CurrentStatus: {statusBeforeRejection}, User: {reservationToReject.User?.Email}";

                if (statusBeforeRejection != "Pending" && statusBeforeRejection != "ModificationRequested")
                {
                    TempData["Message"] = $"This reservation (ID: {id}) is not in a state that can be rejected (Current Status: {statusBeforeRejection})."; MessageType = "info"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Reject Reservation", "Failure - Invalid Status for Rejection", logDetails);
                    return RedirectToPage();
                }

                if (reservationToReject.ModificationRequestDetails != null && reservationToReject.ModificationRequestDetails.Contains("automatically rejected")) // Çevrildi
                {
                    TempData["Message"] = $"This reservation (ID: {id}) has already been rejected by the system due to a public holiday."; MessageType = "info"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Reject Reservation", "Info - Already Auto-Rejected", logDetails);
                    return RedirectToPage();
                }

                await EnsurePublicHolidaysLoadedAsync(reservationToReject.Date.Year);
                bool isHolidayRejectionForOriginalPending = PublicHolidays.TryGetValue(reservationToReject.Date.Date, out var holidayName) && statusBeforeRejection == "Pending";

                string finalStatusAfterRejection; string emailSubject; string emailBodyReasonPart; string tempDataReasonPart = "";

                if (statusBeforeRejection == "ModificationRequested")
                {
                    finalStatusAfterRejection = reservationToReject.PreviousStatus ?? "Pending";
                    emailSubject = "Your Reservation Modification Request Has Been Rejected "; 
                    emailBodyReasonPart = "(The changes you requested could not be applied.)"; // Çevrildi
                    tempDataReasonPart = $" Modification request rejected, reservation reverted to its previous status ('{finalStatusAfterRejection}')."; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Reject Modification Request", "Success", $"ResId={id} modification request rejected. Reverted to {finalStatusAfterRejection}. Original PreviousStatus: {reservationToReject.PreviousStatus}. User: {reservationToReject.User?.Email}");
                    reservationToReject.RequestedNewClassId = null; reservationToReject.RequestedNewDate = null; reservationToReject.RequestedNewTimeSlot = null;
                    reservationToReject.ModificationRequestDetails = "Modification request rejected by admin."; // Çevrildi
                }
                else // Pending
                {
                    finalStatusAfterRejection = "Rejected";
                    emailSubject = "Your Reservation Request Has Been Rejected "; 
                    emailBodyReasonPart = isHolidayRejectionForOriginalPending ? $"because it coincides with the <b>{holidayName} ({reservationToReject.Date:dd.MM.yyyy})</b> public holiday." : "(Rejected by administrator.)"; // Çevrildi
                    tempDataReasonPart = isHolidayRejectionForOriginalPending ? $" Reason: Public Holiday ({holidayName})." : " Rejected by administrator."; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Reject Pending Reservation", "Success", $"ResId={id} pending request rejected. Holiday: {isHolidayRejectionForOriginalPending}. User: {reservationToReject.User?.Email}");
                    if (isHolidayRejectionForOriginalPending && string.IsNullOrEmpty(reservationToReject.ModificationRequestDetails)) reservationToReject.ModificationRequestDetails = $"Rejected due to public holiday ({holidayName})."; 
                    else if (string.IsNullOrEmpty(reservationToReject.ModificationRequestDetails)) reservationToReject.ModificationRequestDetails = "Rejected by administrator."; // Çevrildi
                }

                reservationToReject.Status = finalStatusAfterRejection;
                if (finalStatusAfterRejection == "Rejected") { reservationToReject.PreviousStatus = null; }

                await _context.SaveChangesAsync();

                if (reservationToReject.User != null && !string.IsNullOrEmpty(reservationToReject.User.Email))
                {
                    string emailBody = $"Dear {reservationToReject.User.FullName},<br/><br/>We regret to inform you that your reservation request/modification request (ID: {id}) for classroom <b>{reservationToReject.Class?.Name}</b> (Instructor: {reservationToReject.Instructor?.FullName ?? "N/A"}) on {reservationToReject.Date:dd.MM.yyyy} at {reservationToReject.TimeSlot} has been <span style='color:red; font-weight:bold;'>rejected</span>."; // Çevrildi
                    emailBody += $"<br/>{emailBodyReasonPart}";
                    if (statusBeforeRejection == "ModificationRequested") { emailBody += $" Your reservation status has been updated to '{finalStatusAfterRejection}'."; } // Çevrildi
                    emailBody += "<br/><br/>Thank you,<br/>Reservation System"; // Çevrildi
                    try
                    {
                        await _emailService.SendEmailAsync(reservationToReject.User.Email, emailSubject, emailBody);
                        await _loggingService.LogActionAsync(adminId, "Rejection Email Sent", "Success", $"To: {reservationToReject.User.Email} for ResId {id}");
                    }
                    catch (Exception ex)
                    {
                        await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send rejection email to {reservationToReject.User.Email} for ResId {id}");
                    }
                }
                TempData["Message"] = $"Reservation request (ID: {id}) processed.{tempDataReasonPart}"; MessageType = "warning"; 
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error during OnPostRejectAsync for Res ID {id}. Reservation: {reservationToReject?.Id.ToString() ?? "N/A"}");
                TempData["Message"] = "An unexpected error occurred while rejecting the reservation."; MessageType = "danger"; 
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostApproveCancellationAsync(int id)
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "Approve Cancellation Attempt", "Processing", $"Attempting to approve cancellation for Reservation ID: {id}");
            Reservation? reservationToCancel = null;
            try
            {
                reservationToCancel = await _context.Reservations.Include(r => r.User).Include(r => r.Class).FirstOrDefaultAsync(r => r.Id == id);
                if (reservationToCancel == null)
                {
                    TempData["Message"] = " Reservation for cancellation not found."; MessageType = "danger"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Approve Cancellation", "Failure - Not Found", $"Reservation ID {id} not found.");
                    return RedirectToPage();
                }
                if (reservationToCancel.Status != "Cancellation Requested")
                {
                    TempData["Message"] = $" This reservation (ID: {id}) is not in 'Cancellation Requested' state (Current Status: {reservationToCancel.Status})."; MessageType = "info"; 
                    await _loggingService.LogActionAsync(adminId, "Approve Cancellation", "Failure - Invalid Status", $"ResId {id} is not in 'Cancellation Requested' state. Current: {reservationToCancel.Status}");
                    return RedirectToPage();
                }

                string statusBeforeCancellationReq = reservationToCancel.PreviousStatus ?? "Unknown"; 
                reservationToCancel.Status = "Cancelled";
                reservationToCancel.PreviousStatus = null; reservationToCancel.ModificationRequestDetails = "Cancellation request approved by admin."; 
                reservationToCancel.RequestedNewClassId = null; reservationToCancel.RequestedNewDate = null; reservationToCancel.RequestedNewTimeSlot = null;
                await _context.SaveChangesAsync();
                await _loggingService.LogActionAsync(adminId, "Approve Cancellation", "Success", $"ReservationId={id} (User: {reservationToCancel.User?.Email}) cancellation approved. Status before CxReq: {statusBeforeCancellationReq}. New Status: Cancelled.");

                if (reservationToCancel.User != null && !string.IsNullOrEmpty(reservationToCancel.User.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(reservationToCancel.User.Email, "Your Reservation Cancellation Request Has Been Approved 👍", $"Dear {reservationToCancel.User.FullName},<br/><br/>Your cancellation request for the reservation of classroom <b>{reservationToCancel.Class?.Name}</b> on {reservationToCancel.Date:dd.MM.yyyy} at {reservationToCancel.TimeSlot} has been <span style='color:green; font-weight:bold;'>approved</span>. Your reservation has been cancelled.<br/><br/>Thank you,<br/>Reservation System"); 
                        await _loggingService.LogActionAsync(adminId, "Cancellation Approval Email Sent", "Success", $"To: {reservationToCancel.User.Email} for ResId {id}");
                    }
                    catch (Exception ex)
                    {
                        await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send cancellation approval email to {reservationToCancel.User.Email} for ResId {id}");
                    }
                }
                TempData["Message"] = $" Cancellation request for reservation (ID: {id}) approved. Reservation status set to 'Cancelled'."; MessageType = "success"; // Çevrildi
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error during OnPostApproveCancellationAsync for Res ID {id}. Reservation: {reservationToCancel?.Id.ToString() ?? "N/A"}");
                TempData["Message"] = "An unexpected error occurred while approving the cancellation request."; MessageType = "danger"; 
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectCancellationAsync(int id)
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "Reject Cancellation Attempt", "Processing", $"Attempting to reject cancellation for Reservation ID: {id}");
            Reservation? reservationToReview = null;
            try
            {
                reservationToReview = await _context.Reservations.Include(r => r.User).Include(r => r.Class).FirstOrDefaultAsync(r => r.Id == id);
                if (reservationToReview == null)
                {
                    TempData["Message"] = "Reservation to review not found."; MessageType = "danger"; // Çevrildi
                    await _loggingService.LogActionAsync(adminId, "Reject Cancellation", "Failure - Not Found", $"Reservation ID {id} not found.");
                    return RedirectToPage();
                }
                if (reservationToReview.Status != "Cancellation Requested")
                {
                    TempData["Message"] = $"This reservation (ID: {id}) is not in 'Cancellation Requested' state (Current Status: {reservationToReview.Status})."; MessageType = "info"; 
                    await _loggingService.LogActionAsync(adminId, "Reject Cancellation", "Failure - Invalid Status", $"ResId {id} is not in 'Cancellation Requested' state. Current: {reservationToReview.Status}");
                    return RedirectToPage();
                }

                string statusToRevertTo = reservationToReview.PreviousStatus ?? "Pending";
                reservationToReview.Status = statusToRevertTo;
                reservationToReview.PreviousStatus = null; reservationToReview.ModificationRequestDetails = "Cancellation request rejected by admin, status reverted to previous."; // Çevrildi
                reservationToReview.RequestedNewClassId = null; reservationToReview.RequestedNewDate = null; reservationToReview.RequestedNewTimeSlot = null;

                await _context.SaveChangesAsync();
                await _loggingService.LogActionAsync(adminId, "Reject Cancellation", "Success", $"ReservationId={id} (User: {reservationToReview.User?.Email}) cancellation rejected. Status reverted to '{statusToRevertTo}'.");

                if (reservationToReview.User != null && !string.IsNullOrEmpty(reservationToReview.User.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(reservationToReview.User.Email, "Your Reservation Cancellation Request Has Been Rejected ⚠️", $"Dear {reservationToReview.User.FullName},<br/><br/>Your cancellation request for the reservation of classroom <b>{reservationToReview.Class?.Name}</b> on {reservationToReview.Date:dd.MM.yyyy} at {reservationToReview.TimeSlot} has been <span style='color:red; font-weight:bold;'>rejected</span> by the admin. Your reservation status has been updated to '{statusToRevertTo}'.<br/><br/>Thank you,<br/>Reservation System"); 
                        await _loggingService.LogActionAsync(adminId, "Cancellation Rejection Email Sent", "Success", $"To: {reservationToReview.User.Email} for ResId {id}");
                    }
                    catch (Exception ex)
                    {
                        await _loggingService.LogErrorAsync(ex, adminId, $"Failed to send cancellation rejection email to {reservationToReview.User.Email} for ResId {id}");
                    }
                }
                TempData["Message"] = $"Cancellation request for reservation (ID: {id}) rejected. Reservation status updated to '{statusToRevertTo}'."; MessageType = "warning"; // Çevrildi
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error during OnPostRejectCancellationAsync for Res ID {id}. Reservation: {reservationToReview?.Id.ToString() ?? "N/A"}");
                TempData["Message"] = "An unexpected error occurred while rejecting the cancellation request."; MessageType = "danger"; // Çevrildi
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "Export Reservations", "Attempt");
            try
            {
                var reservationsData = await _context.Reservations
                    .Include(r => r.User).Include(r => r.Instructor).Include(r => r.Class)
                    .Select(r => new
                    {
                        ReservationID = r.Id,
                        RequestingUser = r.User != null ? r.User.FullName : "N/A",
                        RequestingUserEmail = r.User != null ? r.User.Email : "N/A",
                        AssignedInstructor = r.Instructor != null ? r.Instructor.FullName : "N/A",
                        ClassName = r.Class != null ? r.Class.Name : "N/A",
                        Date = r.Date.ToString("dd.MM.yyyy"),
                        TimeSlot = r.TimeSlot,
                        Status = r.Status,
                        TermId = r.TermId,
                        ModificationDetails = r.ModificationRequestDetails,
                        PreviousStatus = r.PreviousStatus,
                        RequestedNewClassId = r.RequestedNewClassId,
                        RequestedNewDate = r.RequestedNewDate,
                        RequestedNewTimeSlot = r.RequestedNewTimeSlot
                    }).ToListAsync();
                var json = JsonSerializer.Serialize(reservationsData, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);

                await _loggingService.LogActionAsync(adminId, "Export Reservations", "Success", $"Exported {reservationsData.Count} reservations.");
                return File(bytes, "application/json", $"reservations_export_{DateTime.Now:yyyyMMddHHmmss}.json");
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, "Error exporting reservations.");
                TempData["Message"] = "An error occurred while exporting reservations."; 
                TempData["MessageType"] = "danger";
                return RedirectToPage();
            }
        }
    }
}