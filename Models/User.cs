using System;
using System.Collections.Generic;

// models/user.cs сущность пользователя приложения
namespace MovieApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // роль: "user" или "admin"
        public string Role { get; set; } = "User";

        // включён ли родительский контроль
        public bool IsParentalControlEnabled { get; set; } = false;

        // мягкое удаление (архивация)
        public bool IsDeleted { get; set; } = false;

        // Аватар в формате Base64 (облачная архитектура)
        public string? AvatarBase64 { get; set; }

        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        public bool IsPrivate { get; set; } = false;

        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }
}