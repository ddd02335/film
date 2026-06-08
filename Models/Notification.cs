using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieApp.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int RecipientId { get; set; }
        public int SenderId { get; set; }
        public string Type { get; set; } = string.Empty; // "MovieShare" or "RatingsShare"
        public int? MovieId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("RecipientId")]
        public User? Recipient { get; set; }

        [ForeignKey("SenderId")]
        public User? Sender { get; set; }

        [ForeignKey("MovieId")]
        public Movie? Movie { get; set; }
    }
}
