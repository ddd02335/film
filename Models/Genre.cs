// models/genre.cs сущность жанра (id генерируется sqlite автоматически)
namespace MovieApp.Models
{
    public class Genre
    {
        // id назначается sqlite автоматически (autoincrement),
        // т.к. кинопоиск возвращает жанры строками, а не числовыми id
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    }
}