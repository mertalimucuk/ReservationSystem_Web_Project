using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models;
using ReservationSystem.Services; 
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Http; 

namespace ReservationSystem.Pages.Admin 
{
    [Authorize(Roles = "admin")] 
    public class ClassListModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService; 

        public ClassListModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService; 
        }

        public List<ClassWithRating> ClassesWithRatings { get; set; } = new();

        private string GetAdminIdentifier()
        {
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? "UnknownAdmin";
        }

        public async Task OnGetAsync()
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "View Class List", "Attempt");

            try
            {
                ClassesWithRatings = await _context.Classes
                    .Include(c => c.Instructor) 
                    .Include(c => c.Feedbacks) 
                    .Select(c => new ClassWithRating
                    {
                        ClassId = c.Id,
                        ClassName = c.Name,
                        Description = c.Description, 
                        InstructorName = c.Instructor != null ? c.Instructor.FullName : "N/A", 
                        AverageRating = c.Feedbacks.Any() ? c.Feedbacks.Average(f => f.Rating) : 0,
                        FeedbackCount = c.Feedbacks.Count() 
                    })
                    .OrderBy(c => c.ClassName) 
                    .ToListAsync();
                
                await _loggingService.LogActionAsync(adminId, "View Class List", "Success", $"Classes loaded: {ClassesWithRatings.Count}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, "Error retrieving class list");
                TempData["ErrorMessage"] = "Sınıf listesi yüklenirken bir hata oluştu.";
                ClassesWithRatings = new List<ClassWithRating>(); 
            }
        }

        public class ClassWithRating
        {
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string? Description { get; set; } // Açıklama alanı
            public string InstructorName { get; set; } = string.Empty; // Eğitmen adı alanı
            public double AverageRating { get; set; }
            public int FeedbackCount { get; set; } // Geri bildirim sayısı
        }
    }
}
