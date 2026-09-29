using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // ForeignKey ve Column attribute'ları için

namespace ReservationSystem.Models
{
    public class Feedback
    {
        public int Id { get; set; }

       
        [Required(ErrorMessage = "Lütfen adınızı ve soyadınızı giriniz.")]
        [StringLength(100)] 
        [Display(Name = "Adınız Soyadınız")]
        public string SubmitterFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lütfen e-posta adresinizi giriniz.")]
        [StringLength(100)] 
        [EmailAddress(ErrorMessage = "Lütfen geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta Adresiniz")]
        public string SubmitterEmail { get; set; } = string.Empty;

        
        public int? UserId { get; set; } 
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // ---- Değerlendirilen Ders/Sınıf Bilgileri ----
        [Required(ErrorMessage = "Please select a class.")]
        public int ClassId { get; set; } 
        [ForeignKey("ClassId")]
        public virtual Class? Class { get; set; }

        
        [Required(ErrorMessage = "Please indicate the reference date of the course.")] 
        [DataType(DataType.Date)]
        [Display(Name = "Reference Course Date")]
        public DateTime SpecificDate { get; set; }

        [Required(ErrorMessage = "Please select the reference time zone of the course.")]
        [StringLength(50)] 
        [Display(Name = "Reference Time Zone")]
        public string SpecificTimeSlot { get; set; } = string.Empty;

        
        [Required(ErrorMessage = "Please give a score between 1-5.")]
        [Range(1, 5, ErrorMessage = "The score must be between 1 and 5")]
        public int Rating { get; set; } 

        [StringLength(1000)] 
        [DataType(DataType.MultilineText)]
        public string? Comment { get; set; } 

        [Required]
        public DateTime FeedbackDate { get; set; } = DateTime.UtcNow;

      
        public int? ReservationId { get; set; } 
        [ForeignKey("ReservationId")]
        public virtual Reservation? Reservation { get; set; }

        
        public int? AssociatedInstructorId { get; set; } 
        [ForeignKey("AssociatedInstructorId")]
        public virtual User? Instructor { get; set; }
    }
}
