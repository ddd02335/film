using System;
using System.Collections.Generic;

namespace MovieApp.Models
{
    /// <summary> сущность для системы тикетов поддержки. </summary>
    public class SupportTicket
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        public string Subject { get; set; } = string.Empty;
        
        public string Status { get; set; } = "Открыто"; // "Открыто", "В работе", "Закрыто"
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string DisplaySender => User != null ? (User.IsDeleted ? $"{User.Login} (Удаленный аккаунт)" : User.Login) : "Неизвестно";

        public virtual User? User { get; set; }
        public virtual ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
    }
}