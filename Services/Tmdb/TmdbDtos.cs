// services/tmdb/tmdbdtos.cs классы для десериализации json-ответов tmdb api
using System.Text.Json.Serialization;

namespace MovieApp.Services.Tmdb
{
    // ответ от get /genre/movie/list
    public class TmdbGenreListResponse
    {
        [JsonPropertyName("genres")]
        public List<TmdbGenreDto> Genres { get; set; } = new();
    }

    public class TmdbGenreDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    // ответ от get /movie/popular
    public class TmdbMovieListResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbMovieDto> Results { get; set; } = new();

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }
    }

    public class TmdbMovieDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        // краткое описание фильма из api
        [JsonPropertyName("overview")]
        public string Overview { get; set; } = string.Empty;

        // дата выхода в формате "2024-07-19" год парсим отдельно
        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        // относительный путь к постеру: "/abc123.jpg" базовый url добавляем сами
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        // список id жанров, к которым относится этот фильм
        [JsonPropertyName("genre_ids")]
        public List<int> GenreIds { get; set; } = new();
    }
}