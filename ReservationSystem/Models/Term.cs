using System;
using System.Collections.Generic; 
using System.ComponentModel.DataAnnotations; 

namespace ReservationSystem.Models
{
    public class Term
    {
        public int Id { get; set; }

        [Required] 
        public string Name { get; set; } = string.Empty;

        [Required] 
        [DataType(DataType.Date)] 
        public DateTime StartDate { get; set; } = new DateTime(DateTime.Now.Year, 1, 1); 

        [Required] 
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = new DateTime(DateTime.Now.Year, 12, 31); 

        
        public bool IsActive { get; set; }
        

        public virtual ICollection<Reservation>? Reservations { get; set; } 
    }
}