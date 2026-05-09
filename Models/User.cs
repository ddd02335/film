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

        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }
}