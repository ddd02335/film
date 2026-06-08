using System;

namespace MovieApp.Models
{
    public class SupportMessage
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public int SenderId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual SupportTicket? Ticket { get; set; }
        public virtual User? Sender { get; set; }
    }
}
