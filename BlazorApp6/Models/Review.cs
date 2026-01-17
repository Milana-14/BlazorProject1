using System.ComponentModel.DataAnnotations;

namespace BlazorApp6.Models
{
    public class Review
    {
        public Guid Id { get; set; }

        [Required]
        public string Comment { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }

        public Guid SenderStudentId { get; set; }
        public Guid ReceiverStudentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Review() 
        {
            Id = Guid.NewGuid();
        }
        public Review(string comment, int rating, Guid senderId, Guid receiverId)
        {
            Id = Guid.NewGuid();
            Comment = comment;
            Rating = rating;
            SenderStudentId = senderId;
            ReceiverStudentId = receiverId;
        }
    }
}
