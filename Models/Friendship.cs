using System.ComponentModel.DataAnnotations.Schema;

namespace MovieApp.Models
{
    public class Friendship
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int FriendId { get; set; }
        public string Status { get; set; } = "Pending"; // "Pending" or "Accepted"

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("FriendId")]
        public User? Friend { get; set; }
    }
}
