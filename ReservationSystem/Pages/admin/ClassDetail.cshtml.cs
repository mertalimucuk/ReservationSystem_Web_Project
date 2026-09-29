using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models;
using ReservationSystem.Services; // LoggingService için
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Http; 


namespace ReservationSystem.Pages.Admin 
{
    [Authorize(Roles = "admin")] 
    public class ClassDetailModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService; 

       
        public ClassDetailModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService; 
        }

        public Class ClassDetail { get; set; } = null!;
        public List<Reservation> WeeklyReservations { get; set; } = new List<Reservation>();
        public List<Feedback> Feedbacks { get; set; } = new List<Feedback>();
        public double AverageRating { get; set; }
        public int FeedbackCount { get; set; }

        private string GetAdminIdentifier()
        {
            
            return HttpContext.Session.GetString("UserId") ?? "UnknownAdmin";
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var adminId = GetAdminIdentifier();

            if (id == null)
            {
                await _loggingService.LogActionAsync(adminId, "View Class Detail Attempt", "Failure - ID Null", "No class ID provided.");
                return NotFound();
            }

            try
            {
                ClassDetail = await _context.Classes
                    .Include(c => c.Instructor)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (ClassDetail == null)
                {
                    await _loggingService.LogActionAsync(adminId, "View Class Detail Attempt", "Failure - Class Not Found", $"Class ID: {id} not found.");
                    return NotFound();
                }

                await _loggingService.LogActionAsync(adminId, "View Class Detail", "Success", $"Class ID: {ClassDetail.Id}, Name: {ClassDetail.Name}");

                Feedbacks = await _context.Feedbacks
                    .Include(f => f.User)
                    .Include(f => f.Reservation)
                        .ThenInclude(r => r!.Instructor)
                    .Where(f => f.ClassId == id)
                    .OrderByDescending(f => f.FeedbackDate)
                    .ToListAsync();

                WeeklyReservations = await _context.Reservations
                    .Include(r => r.Instructor)
                    .Where(r => r.ClassId == id && r.Status == "Approved")
                    .OrderBy(r => r.Date)
                    .ThenBy(r => r.TimeSlot)
                    .ToListAsync();

                if (Feedbacks.Any())
                {
                    AverageRating = Feedbacks.Average(f => f.Rating);
                    FeedbackCount = Feedbacks.Count;
                }
                else
                {
                    AverageRating = 0;
                    FeedbackCount = 0;
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error retrieving details for class ID {id}");
                
                TempData["ErrorMessage"] = "An error occurred while retrieving class details.";
                
                return Page(); 
            }

            return Page();
        }
        //AI PROMPT:"Write an ASP.NET Core Razor Page handler that returns calendar event data in JSON format for a given class ID and date range. Include logging, error handling, and support for time slots."
        public async Task<JsonResult> OnGetCalendarDataAsync(int id, string start, string end)
        {
            var adminId = GetAdminIdentifier(); 
            
            await _loggingService.LogActionAsync(adminId, "Fetch Calendar Data for Class Detail", "Attempt", $"Class ID: {id}, Start: {start}, End: {end}");

            if (!DateTime.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime startDate) ||
                !DateTime.TryParse(end, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime endDate))
            {
                var errorDetail = $"Invalid date format. Start: '{start}', End: '{end}'";
                
                await _loggingService.LogActionAsync(adminId, "Fetch Calendar Data for Class Detail", "Failure - Invalid Date Format", errorDetail);
                
                return new JsonResult(new { error = "Invalid date format." }) { StatusCode = 400 };
            }

            try
            {
                var approvedReservationsForClass = await _context.Reservations
                    .Include(r => r.Instructor)
                    .Where(r => r.ClassId == id &&
                                 r.Status == "Approved" &&
                                 r.Date >= startDate.Date && r.Date < endDate.Date)
                    .ToListAsync();

                var calendarEvents = new List<object>();
                foreach (var r in approvedReservationsForClass)
                {
                    DateTime eventStartDateTime = r.Date;
                    DateTime eventEndDateTime = r.Date.AddHours(1); 
                    bool isAllDay = true;

                    if (!string.IsNullOrEmpty(r.TimeSlot))
                    {
                        var timeParts = r.TimeSlot.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        if (timeParts.Length == 2)
                        {
                            if (TimeSpan.TryParse(timeParts[0].Trim(), CultureInfo.InvariantCulture, out TimeSpan startTimeParsed) &&
                                TimeSpan.TryParse(timeParts[1].Trim(), CultureInfo.InvariantCulture, out TimeSpan endTimeParsed))
                            {
                                eventStartDateTime = r.Date.Add(startTimeParsed);
                                eventEndDateTime = r.Date.Add(endTimeParsed);
                                isAllDay = false;
                            }
                        }
                    }

                    calendarEvents.Add(new
                    {
                        id = r.Id,
                        title = $"Reserved by: {r.Instructor?.FullName ?? "N/A"}",
                        start = eventStartDateTime.ToString("o"), 
                        end = eventEndDateTime.ToString("o"),
                        allDay = isAllDay,
                        color = "purple",
                        extendedProps = new
                        {
                            instructor = r.Instructor?.FullName ?? "N/A",
                            timeSlot = r.TimeSlot,
                            status = r.Status
                        }
                    });
                }
                await _loggingService.LogActionAsync(adminId, "Fetch Calendar Data for Class Detail", "Success", $"Class ID: {id}, Events Found: {calendarEvents.Count}");
                return new JsonResult(calendarEvents);
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error fetching calendar data for class ID {id}");
                return new JsonResult(new { error = "An error occurred while fetching calendar data." }) { StatusCode = 500 };
            }
        }
    }
}
