using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models;
using ReservationSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Session için
using Microsoft.AspNetCore.Authorization; // Authorize attribute için

namespace ReservationSystem.Pages.Instructor
{
    [Authorize(Roles = "instructor")] // Sadece eğitmenler erişebilir
    public class FeedbackFormModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService;

        
        public FeedbackFormModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService; 
        }

        [BindProperty]
        public Feedback Feedback { get; set; } = new Feedback();

        public List<SelectListItem> ReservationOptions { get; set; } = new List<SelectListItem>();

        [TempData]
        public string? Message { get; set; }
        [TempData]
        public string? MessageType { get; set; }

        private string GetInstructorIdentifier()
        {
            
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? "UnknownInstructor";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var instructorIdentifier = GetInstructorIdentifier();
            // Session ve rol kontrolü [Authorize] attribute ile yapılıyor.
            // if (HttpContext.Session.GetString("UserRole") != "instructor" || string.IsNullOrEmpty(instructorIdString))
            // {
            //    await _loggingService.LogActionAsync(instructorIdentifier, "View Feedback Form", "Failure - Unauthorized");
            //    return RedirectToPage("/Login");
            // }

            var instructorIdString = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(instructorIdString, out var instructorId))
            {
                await _loggingService.LogActionAsync(instructorIdentifier, "View Feedback Form", "Failure - Invalid Instructor ID", $"Could not parse Instructor ID from session: {instructorIdString}");
                TempData["Message"] = "Geçersiz eğitmen oturumu."; MessageType = "danger";
                return RedirectToPage("/Error"); 
            }
            
            await _loggingService.LogActionAsync(instructorIdentifier, "View Feedback Form", "Attempt", $"Instructor ID: {instructorId}");
            try
            {
                await LoadReservationOptionsAsync(instructorId);
                await _loggingService.LogActionAsync(instructorIdentifier, "View Feedback Form", "Success", $"Loaded {ReservationOptions.Count} reservation options for Instructor ID: {instructorId}.");
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Error loading reservation options for Feedback Form. Instructor ID: {instructorId}");
                Message = "Geri bildirim formu yüklenirken bir hata oluştu."; MessageType = "danger";
                
                ReservationOptions = new List<SelectListItem>(); 
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var instructorIdentifier = GetInstructorIdentifier(); 
            var instructorName = HttpContext.Session.GetString("UserName") ?? instructorIdentifier; 

            
            var instructorIdString = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(instructorIdString, out var instructorId))
            {
                await _loggingService.LogActionAsync(instructorIdentifier, "Submit Feedback", "Failure - Invalid Instructor ID", $"Invalid instructor ID in session: {instructorIdString}");
                TempData["Message"] = "Geçersiz eğitmen oturumu."; MessageType = "danger";
                return RedirectToPage("/Error"); 
            }
            
            string logDetailsBase = $"ReservationId={Feedback.ReservationId}, Rating={Feedback.Rating}";
            await _loggingService.LogActionAsync(instructorIdentifier, "Submit Feedback Attempt", "Processing", logDetailsBase);

            await LoadReservationOptionsAsync(instructorId); 

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                await _loggingService.LogActionAsync(instructorIdentifier, "Submit Feedback", "Failure - Invalid Model", $"Errors: {string.Join(", ", errors)}. Details: {logDetailsBase}");
              
                return Page();
            }

            try
            {
                Feedback.UserId = instructorId; 
                Feedback.FeedbackDate = DateTime.UtcNow; 

               
                var reservationForFeedback = await _context.Reservations
                                                    .Include(r => r.Class) 
                                                    .FirstOrDefaultAsync(r => r.Id == Feedback.ReservationId);
                
                if (reservationForFeedback == null)
                {
                    await _loggingService.LogActionAsync(instructorIdentifier, "Submit Feedback", "Failure - Reservation Not Found", $"ReservationId {Feedback.ReservationId} not found. Details: {logDetailsBase}");
                    ModelState.AddModelError(string.Empty, "Geri bildirim için seçilen rezervasyon bulunamadı.");
                    return Page();
                }
                if (reservationForFeedback.InstructorId != instructorId)
                {
                     await _loggingService.LogActionAsync(instructorIdentifier, "Submit Feedback", "Failure - Mismatched Instructor", $"Feedback for ResId {Feedback.ReservationId} (Instructor: {reservationForFeedback.InstructorId}) submitted by Instructor {instructorId}. Details: {logDetailsBase}");
                    ModelState.AddModelError(string.Empty, "Bu rezervasyon için geri bildirim gönderme yetkiniz yok.");
                    return Page();
                }

                Feedback.ClassId = reservationForFeedback.ClassId;
                Feedback.AssociatedInstructorId = reservationForFeedback.InstructorId; 
                Feedback.SpecificDate = reservationForFeedback.Date;
                Feedback.SpecificTimeSlot = reservationForFeedback.TimeSlot;


                _context.Feedbacks.Add(Feedback);
                await _context.SaveChangesAsync();
                
                string classNameForLog = reservationForFeedback.Class?.Name ?? "N/A";
                await _loggingService.LogActionAsync(instructorIdentifier, "Submit Feedback", "Success", $"Feedback ID: {Feedback.Id} for ResId={Feedback.ReservationId}, ClassName='{classNameForLog}', ClassId={Feedback.ClassId}, Rating={Feedback.Rating}");

                TempData["Message"] = " Geri bildiriminiz başarıyla gönderildi!";
                TempData["MessageType"] = "success";
                return RedirectToPage(); 
            }
            catch (DbUpdateException dbEx)
            {
                await _loggingService.LogErrorAsync(dbEx, instructorIdentifier, $"Database error submitting feedback. Details: {logDetailsBase}");
                TempData["Message"] = "Geri bildirim gönderilirken bir veritabanı hatası oluştu."; MessageType = "danger";
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"General error submitting feedback. Details: {logDetailsBase}");
                TempData["Message"] = "Geri bildirim gönderilirken beklenmedik bir hata oluştu."; MessageType = "danger";
            }
          
            return Page();
        }

        private async Task LoadReservationOptionsAsync(int instructorId)
        {
          
            try
            {
                
                DateTime today = DateTime.UtcNow.Date;
                ReservationOptions = await _context.Reservations
                    .Where(r => r.InstructorId == instructorId && 
                                r.Status == "Approved" && 
                                r.Date <= today) 
                    .Include(r => r.Class) 
                    .OrderByDescending(r => r.Date)
                    .ThenByDescending(r => r.TimeSlot)
                    .Take(50) 
                    .Select(r => new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = $"{r.Class!.Name} - {r.Date:dd.MM.yyyy} - {r.TimeSlot}" 
                    })
                    .ToListAsync();
                
                if (!ReservationOptions.Any())
                {
                    ReservationOptions.Insert(0, new SelectListItem { Value = "", Text = "Geri bildirim için uygun ders bulunamadı", Disabled = true });
                }
                else
                {
                    ReservationOptions.Insert(0, new SelectListItem { Value = "", Text = "-- Ders Seçiniz --" });
                }
            }
            catch (Exception ex)
            {
                var instructorIdentifier = GetInstructorIdentifier();
                await _loggingService.LogErrorAsync(ex, instructorIdentifier, $"Error loading reservation options for instructor ID {instructorId} in FeedbackForm.");
                ReservationOptions = new List<SelectListItem> { new SelectListItem { Value = "", Text = "Dersler yüklenemedi", Disabled = true } };
            }
        }
    }
}
