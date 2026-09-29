using System;
using System.Collections.Generic; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservationSystem.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please select a class.")] 
        public int ClassId { get; set; }
        [ForeignKey("ClassId")]
        public virtual Class? Class { get; set; }

        [Required(ErrorMessage = "Please select a date.")] 
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Please select a time slot.")] 
        [StringLength(50)] 
        public string TimeSlot { get; set; } = string.Empty;

       
        [StringLength(50)] 
        public string Status { get; set; } = string.Empty;

        [Required]
        public int TermId { get; set; }
        [ForeignKey("TermId")]
        public virtual Term? Term { get; set; }

        [Required]
        public int UserId { get; set; } 
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        public int InstructorId { get; set; } 
        [ForeignKey("InstructorId")]
        public virtual User? Instructor { get; set; }

        
        [StringLength(1000, ErrorMessage = "Modification request notes cannot exceed 1000 characters.")] 
        [Display(Name = "Modification Request Notes")] 
        public string? ModificationRequestDetails { get; set; }

        
        [StringLength(50)]
        public string? PreviousStatus { get; set; }

        
        public int? RequestedNewClassId { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime? RequestedNewDate { get; set; }
        
        [StringLength(50)]
        public string? RequestedNewTimeSlot { get; set; }
       

        public virtual ICollection<Feedback>? FeedbacksReceived { get; set; }

        public Reservation()
        {
            FeedbacksReceived = new HashSet<Feedback>();
            
        }
    }
}
