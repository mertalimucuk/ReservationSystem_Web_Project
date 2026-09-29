using System;
using System.ComponentModel.DataAnnotations;

namespace ReservationSystem.Models
{
    public class SystemLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        
        public string? UserIdentifier { get; set; } 

        [Required]
        [MaxLength(255)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty; 

        public string? Details { get; set; } 
    }
}