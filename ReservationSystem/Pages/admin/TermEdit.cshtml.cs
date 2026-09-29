using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore; 
using ReservationSystem.Models;
using ReservationSystem.Services; 
using System.Threading.Tasks; 
using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Http; 
using System.Linq; 
using System; 

namespace ReservationSystem.Pages.Admin 
{
    [Authorize(Roles = "admin")] // Sadece admin rolündeki kullanıcılar erişebilir
    public class TermEditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService; 

        
        public TermEditModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService; 
        }

        [BindProperty]
        public Term Term { get; set; } = new Term(); 

        [TempData]
        public string? SuccessMessage { get; set; }
        [TempData]
        public string? ErrorMessage { get; set; }

        private string GetAdminIdentifier()
        {
            
            return HttpContext.Session.GetString("UserId") ?? "UnknownAdmin";
        }

        public async Task<IActionResult> OnGetAsync(int? id) 
        {
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "View Term Edit Page Attempt", "Attempt", $"Requested Term ID: {id?.ToString() ?? "NULL"}");

            if (id == null)
            {
                ErrorMessage = "Term ID not provided.";
                await _loggingService.LogActionAsync(adminId, "View Term Edit Page", "Failure - ID Null", ErrorMessage);
                return RedirectToPage("./TermPanel"); 
            }

            try
            {
                Term = await _context.Terms.FirstOrDefaultAsync(t => t.Id == id);

                if (Term == null)
                {
                    ErrorMessage = $"Term with ID {id} not found.";
                    await _loggingService.LogActionAsync(adminId, "View Term Edit Page", "Failure - Term Not Found", $"Term ID: {id}. Error: {ErrorMessage}");
                    return RedirectToPage("./TermPanel"); // TermList'e yönlendir
                }
                await _loggingService.LogActionAsync(adminId, "View Term Edit Page", "Success", $"Loaded Term ID: {Term.Id}, Name: {Term.Name}");
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while retrieving the term.";
                await _loggingService.LogErrorAsync(ex, adminId, $"Error retrieving term with ID {id}.");
                return RedirectToPage("./TermPanel");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var adminId = GetAdminIdentifier();
            string logDetails = $"Attempting to update Term ID: {Term.Id}, Name: {Term.Name}, Active: {Term.IsActive}";
            await _loggingService.LogActionAsync(adminId, "Update Term Attempt", "Attempt", logDetails);

            if (!ModelState.IsValid)
            {
              
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                await _loggingService.LogActionAsync(adminId, "Update Term Attempt", "Failure - Invalid Model", $"Term ID: {Term.Id}. Errors: {string.Join(", ", errors)}");
                return Page();
            }

            var existingTerm = await _context.Terms.FirstOrDefaultAsync(t => t.Id == Term.Id);

            if (existingTerm == null)
            {
                ErrorMessage = $"Term with ID {Term.Id} not found for update.";
                await _loggingService.LogActionAsync(adminId, "Update Term Attempt", "Failure - Term Not Found", ErrorMessage);
                return RedirectToPage("./TermPanel"); 
            }

            
            if (Term.IsActive) 
            {
                var otherActiveTerms = await _context.Terms
                                               .Where(t => t.Id != Term.Id && t.IsActive)
                                               .ToListAsync();
                if (otherActiveTerms.Any())
                {
                    foreach (var otherTerm in otherActiveTerms)
                    {
                        otherTerm.IsActive = false;
                        await _loggingService.LogActionAsync(adminId, "Deactivate Other Term", "Success", $"Deactivated Term ID: {otherTerm.Id} due to activating Term ID: {Term.Id}");
                    }
                    
                }
            }

            
            existingTerm.Name = Term.Name;
            existingTerm.StartDate = Term.StartDate;
            existingTerm.EndDate = Term.EndDate;
            existingTerm.IsActive = Term.IsActive;

            try
            {
                await _context.SaveChangesAsync();
                SuccessMessage = $"Term '{existingTerm.Name}' has been updated successfully.";
                await _loggingService.LogActionAsync(adminId, "Update Term", "Success", $"Updated Term ID: {existingTerm.Id}, Name: {existingTerm.Name}, Active: {existingTerm.IsActive}");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Concurrency error updating Term ID {Term.Id}.");
                if (!await TermExists(Term.Id))
                {
                    ErrorMessage = $"Term with ID {Term.Id} no longer exists (deleted by another user).";
                    await _loggingService.LogActionAsync(adminId, "Update Term Attempt", "Failure - Concurrency Delete", ErrorMessage);
                    return RedirectToPage("./TermPanel");
                }
                else
                {
                    ErrorMessage = "The term was modified by another user. Please reload and try again.";
                    ModelState.AddModelError(string.Empty, ErrorMessage);
                    return Page();
                }
            }
            catch (DbUpdateException ex)
            {
                ErrorMessage = $"An error occurred while updating the term: {ex.InnerException?.Message ?? ex.Message}";
                await _loggingService.LogErrorAsync(ex, adminId, $"Database error updating Term ID {Term.Id}. Error: {ErrorMessage}");
                return Page(); 
            }

            return RedirectToPage("./TermPanel"); 
        }

        private async Task<bool> TermExists(int id)
        {
            return await _context.Terms.AnyAsync(e => e.Id == id);
        }
    }
}
