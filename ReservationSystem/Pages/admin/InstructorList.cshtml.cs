using Microsoft.AspNetCore.Mvc; 
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
    public class InstructorListModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService;

        public InstructorListModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService;
        }

        public List<InstructorWithStats> Instructors { get; set; } = new List<InstructorWithStats>();

        [TempData] 
                   
        public string? ErrorMessage { get; set; }
        
        

        private string GetAdminIdentifier()
        {
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? "UnknownAdmin";
        }

        public async Task OnGetAsync()
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "View Instructor List", "Attempt");

            try
            {
                var instructorsFromDb = await _context.Users
                    .Where(u => u.Role == "instructor")
                    .OrderBy(u => u.FullName)
                    .ToListAsync();

                Instructors = new List<InstructorWithStats>(); 

                foreach (var instructorUser in instructorsFromDb)
                {
                    
                    
                    var ratings = await _context.Feedbacks
                        .Where(f => f.AssociatedInstructorId == instructorUser.Id) 
                        .Select(f => f.Rating) 
                        .ToListAsync();

                    double averageRating = 0;
                    int feedbackCount = ratings.Count;

                    if (feedbackCount > 0)
                    {
                        averageRating = ratings.Average(); 
                    }

                   
                    int approvedSessionCount = await _context.Reservations
                        .CountAsync(r => r.InstructorId == instructorUser.Id && r.Status == "Approved");

                    Instructors.Add(new InstructorWithStats
                    {
                        InstructorId = instructorUser.Id,
                        FullName = instructorUser.FullName ?? "N/A",
                        Email = instructorUser.Email ?? "N/A", 
                        AssignedSessionsCount = approvedSessionCount,
                        AverageRating = averageRating,
                        FeedbackCount = feedbackCount 
                    });
                }
                
                await _loggingService.LogActionAsync(adminId, "View Instructor List", "Success", $"Instructors loaded: {Instructors.Count}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, "Error retrieving instructor list");
                
                ErrorMessage = "An error occurred while retrieving the instructor list."; 
                Instructors = new List<InstructorWithStats>(); 
            }
        }

        public class InstructorWithStats
        {
            public int InstructorId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public int AssignedSessionsCount { get; set; } 
            public double AverageRating { get; set; }
            public int FeedbackCount { get; set; } 
        }
    }
}
