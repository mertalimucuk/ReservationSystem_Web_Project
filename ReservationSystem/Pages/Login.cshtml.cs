using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;      
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models;             
using ReservationSystem.Services;           
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;                          
using Microsoft.AspNetCore.Http;            
using System.Security.Claims;               
using Microsoft.AspNetCore.Authentication;    
using Microsoft.AspNetCore.Authentication.Cookies;

//AI PROMPTS:
//"Design a login page backend in ASP.NET Core Razor Pages that verifies user credentials using PasswordHasher, sets session data, and redirects based on role. Additionally, allow users to submit support tickets and class feedback, and notify all admins via email. Use TempData for messaging and a shared logging service for diagnostics."
//"Create a Razor Pages PageModel in ASP.NET Core that handles user login via email/password, with session and cookie authentication using Identity. Also add two additional POST handlers: one for submitting support requests and one for feedback submissions with class selection, time slots, and rating. Log all user actions and errors using a centralized logging service."
namespace ReservationSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LoggingService _loggingService;
        private readonly EmailService _emailService;

        public LoginModel(ApplicationDbContext context, LoggingService loggingService, EmailService emailService)
        {
            _context = context;
            _loggingService = loggingService;
            _emailService = emailService;
        }

        [BindProperty]
        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public SupportFormModel SupportForm { get; set; } = new();

        [BindProperty]
        public FeedbackFormInputModel FeedbackForm { get; set; } = new();

        public List<SelectListItem> ClassOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> TimeSlotOptions { get; set; } = new List<SelectListItem>();

        private string GetUserIdentifierForLog(string? emailFallback = null)
        {
            return HttpContext.Session.GetString("UserId") ?? User.Identity?.Name ?? emailFallback ?? "Anonymous_Loginpage_User";
        }

        public async Task OnGetAsync()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // This logic is better handled by a redirect if the user tries to access Login when already authenticated.

            }
            await _loggingService.LogActionAsync(GetUserIdentifierForLog(), "View Login Page", "Success");
            LoadClassOptions();
            LoadTimeSlotOptions();
        }

        public async Task<IActionResult> OnPostAsync() // Login Handler
        {
            var userIdentifierForLog = GetUserIdentifierForLog(Email);
            await _loggingService.LogActionAsync(userIdentifierForLog, "Login Attempt", "Processing", $"Attempting login for email: {Email}");

            LoadClassOptions();
            LoadTimeSlotOptions();
            ClearModelStateForOtherForms(nameof(Email), nameof(Password));

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                await _loggingService.LogActionAsync(userIdentifierForLog, "Login Attempt", "Failure - Invalid Model", $"Login form invalid. Errors: {string.Join("; ", errors)}");
                return Page();
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);
                if (user == null)
                {
                    await _loggingService.LogActionAsync(userIdentifierForLog, "Login Attempt", "Failure - User Not Found", $"User with email '{Email}' not found.");
                    TempData["ErrorMessage"] = "User not found."; // Çevrildi
                    return Page();
                }

                var hasher = new PasswordHasher<User>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, Password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Email),
                        new Claim(ClaimTypes.GivenName, user.FullName),
                        new Claim(ClaimTypes.Role, user.Role)
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),

                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    HttpContext.Session.SetString("UserId", user.Id.ToString());
                    HttpContext.Session.SetString("UserRole", user.Role);
                    HttpContext.Session.SetString("UserName", user.FullName);

                    string logStatus = result == PasswordVerificationResult.SuccessRehashNeeded ? "Success (Rehash Needed)" : "Success";
                    await _loggingService.LogActionAsync(user.Email, "User Login", logStatus, $"User '{user.Email}' logged in. Role: {user.Role}.");

                    if (result == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        user.PasswordHash = hasher.HashPassword(user, Password);
                        _context.Users.Update(user);
                        await _context.SaveChangesAsync();
                        await _loggingService.LogActionAsync(user.Email, "Password Rehashed", "Success", $"Password rehashed for user '{user.Email}'.");
                    }

                    return user.Role.ToLower() == "admin"
                        ? RedirectToPage("/AdminPanel")
                        : RedirectToPage("/InstructorPanel");
                }
                else
                {
                    await _loggingService.LogActionAsync(Email, "Login Attempt", "Failure - Invalid Password", $"Invalid password for user '{Email}'.");
                    TempData["ErrorMessage"] = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, Email, $"Error during login attempt for '{Email}'.");
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            var userName = HttpContext.Session.GetString("UserName") ?? User.FindFirstValue(ClaimTypes.GivenName) ?? "Unknown";
            var userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "UnknownID";

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            await _loggingService.LogActionAsync(userId, "User Logout", "Success", $"User '{userName}' logged out.");
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostSendSupportAsync()
        {
            var submitterIdentifier = GetUserIdentifierForLog(SupportForm.Email);
            await _loggingService.LogActionAsync(submitterIdentifier, "Send Support Request Attempt", "Processing", $"From: {SupportForm.Name} ({SupportForm.Email})");

            LoadClassOptions();
            LoadTimeSlotOptions();
            ClearModelStateForOtherForms(nameof(SupportForm));

            if (!TryValidateModel(SupportForm, nameof(SupportForm)))
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                await _loggingService.LogActionAsync(submitterIdentifier, "Send Support Request", "Failure - Invalid Model", $"Support form invalid. Errors: {string.Join("; ", errors)}");
                // Consider setting TempData["ErrorMessage"] for support form specific errors if needed on page reload
                return Page();
            }

            try
            {
                var adminUsers = await _context.Users.Where(u => u.Role.ToLower() == "admin").ToListAsync();
                string emailSubject = $"New Support Request: {SupportForm.Name}";
                string emailBody = $"Hello Admin,<br/><br/>The following support request was submitted:<br/>" +
                                 $"<b>Sender Name:</b> {SupportForm.Name}<br/>" + // Çevrildi
                                 $"<b>Sender Email:</b> {SupportForm.Email}<br/>" + // Çevrildi
                                 $"<b>Message:</b><br/>{SupportForm.Message.Replace("\r\n", "<br />").Replace("\n", "<br />").Replace("\r", "<br />")}<br/><br/>System."; // (ReplaceLineEndings alternatifi)

                foreach (var admin in adminUsers)
                {
                    if (!string.IsNullOrEmpty(admin.Email))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(admin.Email, emailSubject, emailBody);
                            await _loggingService.LogActionAsync(submitterIdentifier, "Support Email Sent to Admin", "Success", $"Support email sent to admin: {admin.Email}. From: {SupportForm.Email}");
                        }
                        catch (Exception ex)
                        {
                            await _loggingService.LogErrorAsync(ex, submitterIdentifier, $"Failed to send support email to admin: {admin.Email}. From: {SupportForm.Email}");
                        }
                    }
                }
                await _loggingService.LogActionAsync(submitterIdentifier, "Send Support Request", "Success", $"Support message from {SupportForm.Name} ({SupportForm.Email}) processed.");
                TempData["Message"] = "Your support message has been sent successfully!";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, submitterIdentifier, $"Error processing support request from {SupportForm.Name} ({SupportForm.Email}).");
                TempData["Message"] = " An error occurred while sending your support message.";
                TempData["MessageType"] = "danger";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendFeedbackAsync()
        {
            var submitterIdentifier = GetUserIdentifierForLog(FeedbackForm.Email);
            await _loggingService.LogActionAsync(submitterIdentifier, "Send Feedback Attempt", "Processing", $"From: {FeedbackForm.Name} ({FeedbackForm.Email}), ClassID: {FeedbackForm.ClassId}, Rating: {FeedbackForm.Rating}");

            LoadClassOptions();
            LoadTimeSlotOptions();
            ClearModelStateForOtherForms(nameof(FeedbackForm));

            if (!TryValidateModel(FeedbackForm, nameof(FeedbackForm)))
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                await _loggingService.LogActionAsync(submitterIdentifier, "Send Feedback", "Failure - Invalid Model", $"Feedback form invalid. Errors: {string.Join("; ", errors)}");

                return Page();
            }

            try
            {
                var feedbackSubmitterUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == FeedbackForm.Email);
                var selectedClass = FeedbackForm.ClassId.HasValue ? await _context.Classes.FindAsync(FeedbackForm.ClassId.Value) : null;

                var feedback = new Feedback
                {
                    SubmitterFullName = FeedbackForm.Name,
                    SubmitterEmail = FeedbackForm.Email,
                    UserId = feedbackSubmitterUser?.Id,
                    ClassId = FeedbackForm.ClassId!.Value, // FeedbackInputViewModel'de ClassId Required
                    SpecificDate = FeedbackForm.SpecificDate ?? DateTime.UtcNow.Date, // FeedbackInputViewModel'de SpecificDate Required
                    SpecificTimeSlot = FeedbackForm.Time, // FeedbackInputViewModel'de Time Required
                    Rating = FeedbackForm.Rating!.Value, // FeedbackInputViewModel'de Rating Required
                    Comment = FeedbackForm.Comment,
                    FeedbackDate = DateTime.UtcNow,
                    AssociatedInstructorId = null
                };

                if (selectedClass != null && FeedbackForm.SpecificDate.HasValue && !string.IsNullOrEmpty(FeedbackForm.Time))
                {
                    var reservationForFeedback = await _context.Reservations
                        .FirstOrDefaultAsync(r => r.ClassId == selectedClass.Id &&
                                                r.Date.Date == FeedbackForm.SpecificDate.Value.Date &&
                                                r.TimeSlot == FeedbackForm.Time &&
                                                r.Status == "Approved");
                    if (reservationForFeedback != null)
                    {
                        feedback.AssociatedInstructorId = reservationForFeedback.InstructorId;
                        feedback.ReservationId = reservationForFeedback.Id;
                    }
                }

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();
                await _loggingService.LogActionAsync(submitterIdentifier, "Feedback Submitted to DB", "Success", $"Feedback ID: {feedback.Id} for ClassId {FeedbackForm.ClassId} by {FeedbackForm.Name}. AssociatedInstructorId: {feedback.AssociatedInstructorId}");

                var adminUsers = await _context.Users.Where(u => u.Role.ToLower() == "admin").ToListAsync();
                string classNameForEmail = selectedClass?.Name ?? "Unknown Class";
                string emailSubject = $"New Feedback: {classNameForEmail} - {FeedbackForm.Rating} Stars";
                string emailBody = $"Hello Admin,<br/><br/>The following feedback was received:<br/>" + // Çevrildi
                                 $"<b>Sender Name:</b> {FeedbackForm.Name}<br/>" + // Çevrildi
                                 $"<b>Sender Email:</b> {FeedbackForm.Email}<br/>" + // Çevrildi
                                 $"<b>Reviewed Class:</b> {classNameForEmail}<br/>" + // Çevrildi
                                 $"<b>Reference Date:</b> {feedback.SpecificDate:dd.MM.yyyy}<br/>" + // Çevrildi
                                 $"<b>Reference Time:</b> {feedback.SpecificTimeSlot}<br/>" + // Çevrildi
                                 $"<b>Rating:</b> {FeedbackForm.Rating}/5<br/>" + // Çevrildi
                                 $"<b>Comment:</b><br/>{FeedbackForm.Comment?.Replace("\r\n", "<br />").Replace("\n", "<br />").Replace("\r", "<br />") ?? "No comment."}<br/><br/>System.";

                foreach (var admin in adminUsers)
                {
                    if (!string.IsNullOrEmpty(admin.Email))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(admin.Email, emailSubject, emailBody);
                            await _loggingService.LogActionAsync(submitterIdentifier, "Feedback Email Sent to Admin", "Success", $"Feedback email sent to admin: {admin.Email}. For ClassID: {FeedbackForm.ClassId}");
                        }
                        catch (Exception ex)
                        {
                            await _loggingService.LogErrorAsync(ex, submitterIdentifier, $"Failed to send feedback email to admin: {admin.Email}. For ClassID: {FeedbackForm.ClassId}");
                        }
                    }
                }
                await _loggingService.LogActionAsync(submitterIdentifier, "Send Feedback", "Success", $"Feedback for ClassId {FeedbackForm.ClassId} processed.");
                TempData["Message"] = "Your feedback has been successfully sent and saved!";
                TempData["MessageType"] = "success";
            }
            catch (DbUpdateException dbEx)
            {
                await _loggingService.LogErrorAsync(dbEx, submitterIdentifier, $"Database error submitting feedback from {FeedbackForm.Name} ({FeedbackForm.Email}).");
                TempData["Message"] = $"A database issue occurred while submitting your feedback.";
                TempData["MessageType"] = "danger";
            }
            catch (Exception ex)
            {
                await _loggingService.LogErrorAsync(ex, submitterIdentifier, $"General error submitting feedback from {FeedbackForm.Name} ({FeedbackForm.Email}).");
                TempData["Message"] = $"An unexpected issue occurred while submitting your feedback.";
                TempData["MessageType"] = "danger";
            }

            return RedirectToPage();
        }

        private void ClearModelStateForOtherForms(params string[] keepPrefixes)
        {
            var keysToClear = ModelState.Keys
                .Where(key => !keepPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || key.Equals(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var key in keysToClear)
            {
                ModelState.Remove(key);
            }
        }

        private void LoadClassOptions()
        {
            if (!_context.Classes.Any()) { ClassOptions = new List<SelectListItem>(); return; }
            ClassOptions = _context.Classes.OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
        }

        private void LoadTimeSlotOptions()
        {
            TimeSlotOptions = new List<SelectListItem> {
                new SelectListItem { Value = "", Text = "-- Select Time Slot --" },

                new SelectListItem { Value = "10:00-11:00", Text = "10:00 - 11:00" },
                new SelectListItem { Value = "11:00-12:00", Text = "11:00 - 12:00" },
                new SelectListItem { Value = "12:00-13:00", Text = "12:00 - 13:00" },
                new SelectListItem { Value = "13:00-14:00", Text = "13:00 - 14:00" },
                new SelectListItem { Value = "14:00-15:00", Text = "14:00 - 15:00" },
                new SelectListItem { Value = "15:00-16:00", Text = "15:00 - 16:00" },
                new SelectListItem { Value = "16:00-17:00", Text = "16:00 - 17:00" },
                 new SelectListItem { Value = "17:00-18:00", Text = "17:00 - 18:00" }
            };
        }


        public class SupportFormModel
        {
            [Required(ErrorMessage = "Full name is required.")]
            [Display(Name = "Full Name")]
            public string Name { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email address is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Message is required.")]
            [MinLength(10, ErrorMessage = "Message must be at least 10 characters long.")]
            public string Message { get; set; } = string.Empty;
        }
        public class FeedbackFormInputModel
        {
            [Required(ErrorMessage = "Full name is required.")]
            [Display(Name = "Full Name")]
            public string Name { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email address is required (for identification).")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [Display(Name = "Email Address")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please select a class.")]
            [Display(Name = "Class Being Reviewed")]
            public int? ClassId { get; set; }

            [Required(ErrorMessage = "Please select the date of the session.")]
            [DataType(DataType.Date)]
            [Display(Name = "Date of Session")]
            public DateTime? SpecificDate { get; set; }

            [Required(ErrorMessage = "Please select a time slot.")]
            [Display(Name = "Time Slot")]
            public string Time { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please provide a rating.")]
            [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
            [Display(Name = "Your Rating (1-5)")]
            public int? Rating { get; set; }

            [Required(ErrorMessage = "Your comment is required.")]
            [MinLength(5, ErrorMessage = "Comment must be at least 5 characters long.")]
            [Display(Name = "Your Comment")]
            [DataType(DataType.MultilineText)]
            [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
            public string Comment { get; set; } = string.Empty;
        }
    }
}