// models/moviegenre.cs связующая таблица "фильмы жанры" (многие-ко-многим)
// первичный ключ составной: (movieid + genreid)
namespace MovieApp.Models
{
    public class MovieGenre
    {
        public int MovieId { get; set; }
        public int GenreId { get; set; }

        // навигационные свойства к родительским сущностям
        public Movie Movie { get; set; } = null!;
        public Genre Genre { get; set; } = null!;
    }
}