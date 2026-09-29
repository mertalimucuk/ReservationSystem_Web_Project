using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using ReservationSystem.Models;
using System.Text;

namespace ReservationSystem.Pages
{
    public class GenerateHashModel : PageModel
    {
        public string HashedPassword { get; set; }

        public void OnGet()
        {
            var user = new User { FullName = "Admin User", Email = "admin@test.com", Role = "admin" };
            var hasher = new PasswordHasher<User>();
            HashedPassword = hasher.HashPassword(user, "123456");
        }
    }
}
