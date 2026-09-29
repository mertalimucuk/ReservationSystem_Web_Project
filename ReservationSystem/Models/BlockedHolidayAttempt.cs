using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReservationSystem.Models
{
    public class BlockedHolidayAttempt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InstructorId { get; set; } // Denemeyi yapan eğitmenin ID'si

        [ForeignKey("InstructorId")]
        public virtual User? Instructor { get; set; } 

        [Required]
        public int ClassId { get; set; } // Talep edilen sınıfın ID'si

        [ForeignKey("ClassId")]
        public virtual Class? Class { get; set; } 

        [Required]
        [DataType(DataType.Date)]
        public DateTime RequestedDate { get; set; } 

        [Required]
        [StringLength(50)]
        public string RequestedTimeSlot { get; set; } = string.Empty; 

        [Required]
        [StringLength(200)]
        public string HolidayName { get; set; } = string.Empty; // Engellenme sebebi olan resmi tatilin adı

        [Required]
        public DateTime AttemptTimestamp { get; set; } // Denemenin yapıldığı zaman

        public BlockedHolidayAttempt()
        {
            AttemptTimestamp = DateTime.Now;
        }
    }
}
