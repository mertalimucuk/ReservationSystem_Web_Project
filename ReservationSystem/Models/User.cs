using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservationSystem.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full name is required.")] 
        [Display(Name = "Full Name")] 
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")] 
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")] 
        public string Email { get; set; } = string.Empty;

        [Required] 
        public string PasswordHash { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")] 
        public string Role { get; set; } = string.Empty; 

        
        [InverseProperty("User")] 
        public virtual ICollection<Reservation>? BookedReservations { get; set; }

        
        [InverseProperty("Instructor")] 
        public virtual ICollection<Reservation>? TaughtReservations { get; set; }

        
        [InverseProperty("User")] 
        public virtual ICollection<Feedback>? FeedbacksGiven { get; set; }

        
        public virtual ICollection<Class>? PrimaryClassesManaged { get; set; }

        public User()
        {
            BookedReservations = new HashSet<Reservation>();
            TaughtReservations = new HashSet<Reservation>();
            FeedbacksGiven = new HashSet<Feedback>();
            PrimaryClassesManaged = new HashSet<Class>();
        }
    }
}