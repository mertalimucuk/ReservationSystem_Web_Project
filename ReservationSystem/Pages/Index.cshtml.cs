using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ReservationSystem.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public string? UserName { get; set; }

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            // Kullanıcı login olmamışsa, Login sayfasına yönlendir
            if (HttpContext.Session.GetString("UserId") == null)
            {
                return RedirectToPage("/Login");
            }

            // Kullanıcı login olmuşsa ismini al
            UserName = HttpContext.Session.GetString("UserName");

            return Page();
        }

        public IActionResult OnPostLogout()
        {
            // Session'ı temizle, kullanıcıyı çıkış yaptır
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}
