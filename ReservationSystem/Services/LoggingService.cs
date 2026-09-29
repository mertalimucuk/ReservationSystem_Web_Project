using System;
using System.Threading.Tasks; 
using ReservationSystem.Models; 
using Microsoft.Extensions.Logging; 
using Microsoft.AspNetCore.Http; 

//"AI PROMPT:Create a logging service class in ASP.NET Core that logs user actions and exceptions to a database using Entity Framework, with fallback to file logging if DB fails."

namespace ReservationSystem.Services
{
    public class LoggingService
    {
        private readonly ApplicationDbContext _context;

        private readonly IHttpContextAccessor _httpContextAccessor;


        public LoggingService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor = null)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogActionAsync(string userIdentifier, string action, string status, string? details = null)
        {
            var logEntry = new SystemLog
            {
                Timestamp = DateTime.UtcNow,
                UserIdentifier = userIdentifier,
                Action = action,
                Status = status,
                Details = details
            };

            _context.SystemLogs.Add(logEntry);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Failed to log action to database: {ex.Message}");
            }
        }

        public async Task LogErrorAsync(Exception exception, string? userIdentifier = null, string? customMessage = null)
        {

            if (string.IsNullOrEmpty(userIdentifier) && _httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                userIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;
            }

            var errorLogEntry = new ErrorLog
            {
                Timestamp = DateTime.UtcNow,
                UserIdentifier = userIdentifier,
                ErrorMessage = customMessage ?? exception.Message,
                StackTrace = exception.StackTrace,
                RequestMethod = _httpContextAccessor?.HttpContext?.Request.Method,
                RequestPath = _httpContextAccessor?.HttpContext?.Request.Path
            };

            _context.ErrorLogs.Add(errorLogEntry);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                Console.WriteLine($"Failed to log error to database: {dbEx.Message}");

                try
                {
                    string fallbackErrorLogPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "fallback_error.log");
                    string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DB_LOG_FAIL: User={userIdentifier}, Error={exception.Message}, DB_EX={dbEx.Message}{Environment.NewLine}";
                    System.IO.File.AppendAllText(fallbackErrorLogPath, message);
                }
                catch { }
            }
        }

    }
}
