using System;

namespace MovieApp.Models
{
    /// <summary> сущность для системы тикетов поддержки. позволяет пользователям сообщать об ошибках в контенте. </summary>
    public class SupportTicket
    {
        public int Id { get; set; }
        
        /// <summary> id пользователя, создавшего жалобу. </summary>
        public int UserId { get; set; }
        
        /// <summary> причина обращения. </summary>
        public string Reason { get; set; } = "Другое";

        /// <summary> текст сообщения об ошибке. </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary> статус: "открыт" или "закрыт". </summary>
        public string Status { get; set; } = "Открыт";

        /// <summary> ответ администратора. </summary>
        public string? AdminReply { get; set; }
        
        /// <summary> дата и время создания. </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // навигационное свойство (опционально, для отображения логина в админке)
        public virtual User? User { get; set; }
    }
}