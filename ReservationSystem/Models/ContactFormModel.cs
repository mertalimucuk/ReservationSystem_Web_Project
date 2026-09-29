using System.ComponentModel.DataAnnotations;

namespace ReservationSystem.Models
{
    public class FeedbackFormModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a class.")]
        public int? ClassId { get; set; }

        [Required(ErrorMessage = "Please enter time.")]
        public string Time { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rating is required.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int? Rating { get; set; }

        [Required(ErrorMessage = "Comment is required.")]
        [StringLength(1000, MinimumLength = 5)]
        public string Comment { get; set; } = string.Empty;
    }

    public class SupportFormModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message is required.")]
        [StringLength(1000, MinimumLength = 5)]
        public string Message { get; set; } = string.Empty;
    }
}
