using Microsoft.AspNetCore.Identity;
using ReservationSystem.Models;

class HasherTool
{
    static void Main()
    {
        var user = new User(); // boş user
        var hasher = new PasswordHasher<User>();
        var hashedPassword = hasher.HashPassword(user, "123456");

        Console.WriteLine("HASHLENMİŞ ŞİFRE:");
        Console.WriteLine(hashedPassword);
    }
}
