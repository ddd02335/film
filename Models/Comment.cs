using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieApp.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int MovieId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("MovieId")]
        public Movie? Movie { get; set; }
    }
}
