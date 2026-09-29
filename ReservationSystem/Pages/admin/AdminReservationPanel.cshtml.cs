using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models;
using ReservationSystem.Services; 
using Microsoft.AspNetCore.Identity; 
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks; 
using Microsoft.AspNetCore.Http; 
using Microsoft.AspNetCore.Authorization; 


namespace ReservationSystem.Pages.admin 
{
    [Authorize(Roles = "admin")] 
    public class AdminReservationPanelModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService; 

        
        public AdminReservationPanelModel(ApplicationDbContext context, LoggingService loggingService)
        {
            _context = context;
            _loggingService = loggingService; 
        }

        public List<User> Instructors { get; set; } = new();

        [BindProperty]
        public User NewInstructor { get; set; } = new();

        [BindProperty]
        public string PlaintextPassword { get; set; } = string.Empty; 

        [BindProperty]
        public User EditInstructor { get; set; } = new();

        private string GetAdminIdentifier()
        {
           
            return HttpContext.Session.GetString("UserId") ?? "UnknownAdmin";
        }

        public async Task<IActionResult> OnGetAsync()
        {
           
            var adminId = GetAdminIdentifier();
            await _loggingService.LogActionAsync(adminId, "View Admin Instructor Panel", "Success");

            Instructors = await _context.Users
                .Where(u => u.Role == "instructor")
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            var adminId = GetAdminIdentifier();

            if (string.IsNullOrWhiteSpace(NewInstructor.FullName) ||
                string.IsNullOrWhiteSpace(NewInstructor.Email) ||
                string.IsNullOrWhiteSpace(PlaintextPassword))
            {
                TempData["ErrorMessage"] = "Please fill all fields for the new instructor.";
                await _loggingService.LogActionAsync(adminId, "Add Instructor Attempt", "Failure - Missing Fields", $"Email: {NewInstructor.Email}");
                
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }
            
            
            bool emailExists = await _context.Users.AnyAsync(u => u.Email == NewInstructor.Email);
            if (emailExists)
            {
                TempData["ErrorMessage"] = $"Instructor with email '{NewInstructor.Email}' already exists.";
                await _loggingService.LogActionAsync(adminId, "Add Instructor Attempt", "Failure - Email Exists", $"Email: {NewInstructor.Email}");
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }

            var hasher = new PasswordHasher<User>();
            NewInstructor.PasswordHash = hasher.HashPassword(NewInstructor, PlaintextPassword);
            NewInstructor.Role = "instructor";

            try
            {
                _context.Users.Add(NewInstructor);
                await _context.SaveChangesAsync();
                await _loggingService.LogActionAsync(adminId, "Instructor Added", "Success", $"Instructor Email: {NewInstructor.Email}, ID: {NewInstructor.Id}");

                try
                {
                    await SendEmailAsync(NewInstructor.Email, "Instructor Account Created",
                        $"Dear {NewInstructor.FullName},\n\nYour instructor account has been created.\nEmail: {NewInstructor.Email}\nPassword: {PlaintextPassword}\n\nPlease login to the system.");
                    TempData["SuccessMessage"] = "Instructor added and notified by email.";
                    await _loggingService.LogActionAsync(adminId, "Instructor Added - Email Sent", "Success", $"To: {NewInstructor.Email}");
                }
                catch (Exception emailEx)
                {
                    TempData["WarningMessage"] = "Instructor added, but notification email could not be sent.";
                  
                    await _loggingService.LogErrorAsync(emailEx, adminId, $"Failed to send account creation email to {NewInstructor.Email} for instructor ID {NewInstructor.Id}");
                }
            }
            catch (Exception dbEx)
            {
                TempData["ErrorMessage"] = "Error adding instructor to the database.";
                await _loggingService.LogErrorAsync(dbEx, adminId, $"Database error while adding instructor: {NewInstructor.Email}");
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }
            
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var adminId = GetAdminIdentifier();
            var instructor = await _context.Users.FindAsync(id);

            if (instructor != null && instructor.Role == "instructor")
            {
                try
                {
                    _context.Users.Remove(instructor);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Instructor '{instructor.FullName}' deleted.";
                    await _loggingService.LogActionAsync(adminId, "Instructor Deleted", "Success", $"Instructor Email: {instructor.Email}, ID: {instructor.Id}");
                }
                catch (Exception dbEx)
                {
                    TempData["ErrorMessage"] = "Error deleting instructor from the database.";
                    await _loggingService.LogErrorAsync(dbEx, adminId, $"Database error while deleting instructor ID {id}");
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Instructor not found or user is not an instructor.";
                await _loggingService.LogActionAsync(adminId, "Delete Instructor Attempt", "Failure - Not Found or Invalid Role", $"Attempted ID: {id}");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            var adminId = GetAdminIdentifier();

          
            if (string.IsNullOrWhiteSpace(EditInstructor.FullName) || string.IsNullOrWhiteSpace(EditInstructor.Email))
            {
                TempData["ErrorMessage"] = "Instructor name and email cannot be empty for editing.";
                await _loggingService.LogActionAsync(adminId, "Edit Instructor Attempt", "Failure - Missing Fields", $"Attempted ID: {EditInstructor.Id}");
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }

            var existing = await _context.Users.FindAsync(EditInstructor.Id);
            if (existing == null || existing.Role != "instructor")
            {
                TempData["ErrorMessage"] = "Instructor not found for editing.";
                await _loggingService.LogActionAsync(adminId, "Edit Instructor Attempt", "Failure - Not Found", $"Attempted ID: {EditInstructor.Id}");
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }

            
            if (existing.Email != EditInstructor.Email && await _context.Users.AnyAsync(u => u.Email == EditInstructor.Email && u.Id != EditInstructor.Id))
            {
                TempData["ErrorMessage"] = $"Cannot update. Email '{EditInstructor.Email}' is already in use by another account.";
                await _loggingService.LogActionAsync(adminId, "Edit Instructor Attempt", "Failure - Email Exists", $"Instructor ID: {EditInstructor.Id}, New Email: {EditInstructor.Email}");
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }

            existing.FullName = EditInstructor.FullName;
            existing.Email = EditInstructor.Email;
            

            try
            {
                await _context.SaveChangesAsync();
                await _loggingService.LogActionAsync(adminId, "Instructor Edited", "Success", $"Instructor Email: {existing.Email}, ID: {existing.Id}");

                try
                {
                    await SendEmailAsync(existing.Email, "Your Instructor Account Updated",
                        $"Hello {existing.FullName},\n\nYour instructor account information has been updated by the admin.\n\nIf you did not request this change or have concerns, please contact the administration.");
                    TempData["SuccessMessage"] = "Instructor updated and notified by email.";
                    await _loggingService.LogActionAsync(adminId, "Instructor Edited - Email Sent", "Success", $"To: {existing.Email}");
                }
                catch (Exception emailEx)
                {
                    TempData["WarningMessage"] = "Instructor updated, but notification email could not be sent.";
                    await _loggingService.LogErrorAsync(emailEx, adminId, $"Failed to send account update email to {existing.Email} for instructor ID {existing.Id}");
                }
            }
            catch (Exception dbEx)
            {
                TempData["ErrorMessage"] = "Error updating instructor in the database.";
                await _loggingService.LogErrorAsync(dbEx, adminId, $"Database error while editing instructor ID {EditInstructor.Id}");
                Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                return Page();
            }

            return RedirectToPage();
        }

            // AI Prompt: "Can you provide me with a C# private asynchronous method named `SendEmailAsync`? This method should send an email and take three string parameters: `toEmail`, `subject`, and `body'
        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {

            var fromAddress = new MailAddress("cengweb382@gmail.com", "CENG382 Reservation System");
            var toAddress = new MailAddress(toEmail);
            const string fromPassword = "your_google_app_password";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com", // SMTP sunucunuz
                Port = 587, // Genellikle 587 (TLS) veya 465 (SSL)
                EnableSsl = true, // SSL/TLS kullanılıyorsa true
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false // E-posta içeriği HTML ise true yapın
            };
            await smtp.SendMailAsync(message);
        }
    }
}
