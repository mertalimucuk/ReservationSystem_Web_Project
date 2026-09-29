using System;
using System.ComponentModel.DataAnnotations;

namespace ReservationSystem.Models
{
    public class ErrorLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        // Hata kullanıcı kaynaklıysa kullanıcı kimliği
        public string? UserIdentifier { get; set; }

        [Required]
        public string ErrorMessage { get; set; } = string.Empty;

        public string? StackTrace { get; set; }

        // İsteğe bağlı olarak hatanın oluştuğu sayfa veya metodu gözlemlemek için yazdım
        public string? RequestMethod { get; set; }
        public string? RequestPath { get; set; }
    }
}
