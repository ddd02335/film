// models/rating.cs оценка фильма пользователем (1–5)
// первичный ключ составной: (userid + movieid) один пользователь оценивает фильм один раз
namespace MovieApp.Models
{
    public class Rating
    {
        public int UserId { get; set; }
        public int MovieId { get; set; }

        // оценка от 1 до 5 (ограничение задаётся через fluent api)
        public int Score { get; set; }

        // личная заметка пользователя о фильме
        public string? PersonalNote { get; set; }

        // навигационные свойства
        public User User { get; set; } = null!;
        public Movie Movie { get; set; } = null!;
    }
}