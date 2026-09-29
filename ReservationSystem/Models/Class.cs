using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservationSystem.Models
{
    public class Class
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Class name is required.")]
        [StringLength(100, ErrorMessage = "Class name must be max 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description must be max 500 characters.")]
        public string? Description { get; set; }

        
        public int? InstructorId { get; set; }

        [ForeignKey("InstructorId")]
        public User? Instructor { get; set; }  

        public ICollection<Reservation>? Reservations { get; set; }
        public ICollection<Feedback>? Feedbacks { get; set; }
    }
}
