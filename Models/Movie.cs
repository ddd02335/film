// models/movie.cs сущность фильма (id = kinopoiskid, берётся из api кинопоиска)
namespace MovieApp.Models
{
    public class Movie
    {
        // id не генерируется автоматически используем kinopoiskid из api
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Year { get; set; }
        public string? PosterUrl { get; set; }
        public double? RatingKinopoisk { get; set; }
        public string? Type { get; set; }
        public string? AgeRating { get; set; }

        // навигационные свойства для связей многие-ко-многим
        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }
}