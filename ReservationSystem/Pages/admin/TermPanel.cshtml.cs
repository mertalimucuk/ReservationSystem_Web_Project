using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models; 
using ReservationSystem.Services; 
using System; 
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Http; 

//AI PROMPT: Generate a Razor Page model in ASP.NET Core that allows an admin to manage academic terms (CRUD operations), ensures only one active term, uses session and role-based authorization, and includes logging for each action."


namespace ReservationSystem.Pages.Admin
{
    [Authorize(Roles = "admin")]
    public class TermPanelModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService;

        public TermPanelModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService;
        }

        public List<Term> Terms { get; set; } = new();

        [BindProperty]
        public Term NewTerm { get; set; } = new() { StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(4), IsActive = false };

        [TempData]
        public string? Message { get; set; }
        [TempData]
        public string? MessageType { get; set; }

        private string GetAdminIdentifier()
        {
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? "UnknownAdmin";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var adminId = GetAdminIdentifier();

            await _loggingService.LogActionAsync(adminId, "View Term Panel", "Attempt");
            try
            {
                Terms = await _context.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
                await _loggingService.LogActionAsync(adminId, "View Term Panel", "Success", $"Terms loaded: {Terms.Count}");
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, "Error retrieving terms for Term Panel.");
                Message = "An error occurred while loading terms.";
                MessageType = "danger";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            var adminId = GetAdminIdentifier();


            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                await _loggingService.LogActionAsync(adminId, "Add Term Attempt", "Failure - Invalid Model", $"Errors: {string.Join(", ", errors)}");
                Terms = await _context.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
                return Page();
            }

            if (NewTerm.EndDate <= NewTerm.StartDate)
            {
                ModelState.AddModelError("NewTerm.EndDate", "End date must be after start date.");
                await _loggingService.LogActionAsync(adminId, "Add Term Attempt", "Failure - Invalid Date Range", $"Term Name: {NewTerm.Name}, Start: {NewTerm.StartDate}, End: {NewTerm.EndDate}");
                Terms = await _context.Terms.OrderByDescending(t => t.StartDate).ToListAsync();
                return Page();
            }

            string logDetails = $"Attempting to add Term: {NewTerm.Name}, Active: {NewTerm.IsActive}";
            await _loggingService.LogActionAsync(adminId, "Add Term Attempt", "Processing", logDetails);

            try
            {
                if (NewTerm.IsActive)
                {
                    var otherActiveTerms = await _context.Terms.Where(t => t.IsActive).ToListAsync();
                    if (otherActiveTerms.Any())
                    {
                        foreach (var activeTerm in otherActiveTerms)
                        {
                            activeTerm.IsActive = false;
                            await _loggingService.LogActionAsync(adminId, "Deactivate Other Term (During Add)", "Success", $"Deactivated Term ID: {activeTerm.Id} due to activating new Term: {NewTerm.Name}");
                        }
                    }
                }

                _context.Terms.Add(NewTerm);
                await _context.SaveChangesAsync();

                await _loggingService.LogActionAsync(adminId, "Add Term", "Success", $"New term '{NewTerm.Name}' added. ID: {NewTerm.Id}, Active: {NewTerm.IsActive}");
                TempData["Message"] = $"New term '{NewTerm.Name}' added successfully!";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Error adding new term: {NewTerm.Name}");
                TempData["Message"] = "An error occurred while adding the term.";
                TempData["MessageType"] = "danger";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int? id)
        {
            var adminId = GetAdminIdentifier();


            if (id == null)
            {
                TempData["Message"] = "Term ID not provided for deletion.";
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(adminId, "Delete Term Attempt", "Failure - ID Null", "No Term ID provided for deletion.");
                return RedirectToPage();
            }

            await _loggingService.LogActionAsync(adminId, "Delete Term Attempt", "Processing", $"Attempting to delete Term ID: {id}");

            var termToDelete = await _context.Terms
                                           .Include(t => t.Reservations)
                                           .FirstOrDefaultAsync(t => t.Id == id);

            if (termToDelete == null)
            {
                TempData["Message"] = $"Term with ID {id} not found.";
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(adminId, "Delete Term Attempt", "Failure - Not Found", $"Term ID {id} not found for deletion.");
                return RedirectToPage();
            }

            if (termToDelete.Reservations != null && termToDelete.Reservations.Any())
            {
                TempData["Message"] = $"Term '{termToDelete.Name}' cannot be deleted. It has {termToDelete.Reservations.Count} associated reservation(s). Please delete or reassign these reservations first.";
                TempData["MessageType"] = "warning";
                await _loggingService.LogActionAsync(adminId, "Delete Term Attempt", "Failure - Blocked by Reservations", $"Term ID {id} ('{termToDelete.Name}') blocked due to {termToDelete.Reservations.Count} existing reservations.");
                return RedirectToPage();
            }

            try
            {
                _context.Terms.Remove(termToDelete);
                await _context.SaveChangesAsync();
                TempData["Message"] = $"Term '{termToDelete.Name}' has been successfully deleted.";
                TempData["MessageType"] = "success";
                await _loggingService.LogActionAsync(adminId, "Delete Term", "Success", $"Term ID {id} ('{termToDelete.Name}') successfully deleted.");
            }
            catch (DbUpdateException ex)
            {
                TempData["Message"] = $"An error occurred while deleting the term: {ex.InnerException?.Message ?? ex.Message}";
                TempData["MessageType"] = "danger";
                await _loggingService.LogErrorAsync(ex, adminId, $"Error deleting Term ID {id}: {ex.InnerException?.Message ?? ex.Message}");
            }
            return RedirectToPage();
        }


        public async Task<IActionResult> OnPostEditAsync(int id, string name, DateTime startDate, DateTime endDate, bool isActive)
        {
            var adminId = GetAdminIdentifier();


            string logDetails = $"Attempting to edit Term ID: {id}, New Name: {name}, Start: {startDate}, End: {endDate}, Active: {isActive}";
            await _loggingService.LogActionAsync(adminId, "Edit Term Attempt", "Processing", logDetails);

            var termToUpdate = await _context.Terms.FindAsync(id);
            if (termToUpdate == null)
            {
                TempData["Message"] = $"Term with ID {id} not found for update.";
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(adminId, "Edit Term Attempt", "Failure - Not Found", $"Term ID {id} not found for update.");
                return RedirectToPage();
            }

            if (endDate <= startDate)
            {
                TempData["Message"] = "End date must be after start date for the term being edited.";
                TempData["MessageType"] = "danger";
                await _loggingService.LogActionAsync(adminId, "Edit Term Attempt", "Failure - Invalid Date Range", $"Term ID: {id}, Start: {startDate}, End: {endDate}");
                return RedirectToPage();
            }

            try
            {
                if (isActive && !termToUpdate.IsActive)
                {
                    var otherActiveTerms = await _context.Terms.Where(t => t.Id != id && t.IsActive).ToListAsync();
                    if (otherActiveTerms.Any())
                    {
                        foreach (var activeTerm in otherActiveTerms)
                        {
                            activeTerm.IsActive = false;
                            await _loggingService.LogActionAsync(adminId, "Deactivate Other Term (During Edit)", "Success", $"Deactivated Term ID: {activeTerm.Id} due to activating Term ID: {id}");
                        }
                    }
                }

                termToUpdate.Name = name;
                termToUpdate.StartDate = startDate;
                termToUpdate.EndDate = endDate;
                termToUpdate.IsActive = isActive;

                await _context.SaveChangesAsync();
                TempData["Message"] = $"Term '{termToUpdate.Name}' updated successfully!";
                TempData["MessageType"] = "success";
                await _loggingService.LogActionAsync(adminId, "Edit Term", "Success", $"Term ID {id} ('{termToUpdate.Name}') updated. Active: {isActive}");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await _loggingService.LogErrorAsync(ex, adminId, $"Concurrency error updating Term ID {id}.");

                TempData["Message"] = "The term was modified by another user. Please reload and try again.";
                TempData["MessageType"] = "warning";
            }
            catch (DbUpdateException ex)
            {
                TempData["Message"] = $"An error occurred while updating the term: {ex.InnerException?.Message ?? ex.Message}";
                TempData["MessageType"] = "danger";
                await _loggingService.LogErrorAsync(ex, adminId, $"Error updating Term ID {id}: {ex.InnerException?.Message ?? ex.Message}");
            }
            return RedirectToPage();
        }
    }
}
