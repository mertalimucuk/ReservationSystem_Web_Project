using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization; 
using ReservationSystem.Models;
using ReservationSystem.Services; 
using System.Threading.Tasks; 
using System.Security.Claims; 

namespace ReservationSystem.Pages.Admin
{
    [Authorize(Roles = "admin")] 
    public class AddClassModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService; 

        
        public AddClassModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService; 
        }

        [BindProperty]
        public Class Class { get; set; } = new();

        public IActionResult OnGet()
        {
            
            return Page();
        }

        
        public async Task<IActionResult> OnPostAsync()
        {
            var adminUserEmail = User.FindFirstValue(ClaimTypes.Name); 

            if (!ModelState.IsValid)
            {
                
                await _loggingService.LogActionAsync(adminUserEmail, "Add Class Attempt", "Failure - Invalid Model", $"Class Name Attempted: {Class.Name}");
                return Page();
            }

            try
            {
                _context.Classes.Add(Class);
                await _context.SaveChangesAsync(); 

               
                await _loggingService.LogActionAsync(adminUserEmail, "Class Added", "Success", $"Class ID: {Class.Id}, Name: {Class.Name}");

                TempData["Message"] = "Class added successfully!";
                return RedirectToPage("/Admin/ClassList"); 
            }
            catch (Exception ex)
            {
               
                await _loggingService.LogErrorAsync(ex, adminUserEmail, $"Error adding class: {Class.Name}");
                ModelState.AddModelError(string.Empty, "An error occurred while adding the class. Please try again.");
                TempData["ErrorMessage"] = "Error adding class."; 
                return Page();
            }
        }
    }
}