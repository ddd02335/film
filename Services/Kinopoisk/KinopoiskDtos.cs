// services/kinopoisk/kinopoiskdtos.cs dto для маппинга ответа кинопоиска
// эндпоинт: get /api/v2.2/films/collections?type=top_popular_all&page={n}
using System.Text.Json.Serialization;

namespace MovieApp.Services.Kinopoisk
{
    /// <summary> корневой объект ответа от /api/v2.2/films/collections. содержит общее количество страниц и список фильмов на текущей странице. </summary>
    public sealed class KinopoiskCollectionResponse
    {
        /// <summary>общее количество страниц в коллекции.</summary>
        [JsonPropertyName("totalPages")]
        public int TotalPages { get; init; }

        /// <summary>список фильмов на текущей странице.</summary>
        [JsonPropertyName("items")]
        public List<KinopoiskFilmItem> Items { get; init; } = new();
    }

    /// <summary> один фильм из массива items. </summary>
    public sealed class KinopoiskFilmItem
    {
        /// <summary>уникальный идентификатор фильма в кинопоиске.</summary>
        [JsonPropertyName("kinopoiskId")]
        public int KinopoiskId { get; init; }

        /// <summary>название фильма на русском языке.</summary>
        [JsonPropertyName("nameRu")]
        public string? NameRu { get; init; }

        /// <summary>год выхода фильма.</summary>
        [JsonPropertyName("year")]
        public int? Year { get; init; }

        /// <summary>ссылка на постер фильма.</summary>
        [JsonPropertyName("posterUrl")]
        public string? PosterUrl { get; init; }

        /// <summary>тип видео (film, video, tv_series, mini_series )</summary>
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        /// <summary> список жанров фильма. каждый объект содержит только строковое поле "genre". </summary>
        [JsonPropertyName("genres")]
        public List<KinopoiskGenreItem> Genres { get; init; } = new();
    }

    /// <summary> объект жанра внутри массива genres фильма. кинопоиск возвращает жанр как { "genre": "драма" }, а не числовой id. </summary>
    public sealed class KinopoiskGenreItem
    {
        /// <summary>название жанра на русском языке.</summary>
        [JsonPropertyName("genre")]
        public string? Genre { get; init; }
    }

    /// <summary> ответ от детального эндпоинта /api/v2.2/films/{id}. содержит расширенную информацию о фильме (описание и рейтинг). </summary>
    public sealed class KinopoiskFilmDetailResponse
    {
        /// <summary>подробное описание фильма.</summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>рейтинг фильма на кинопоиске.</summary>
        [JsonPropertyName("ratingKinopoisk")]
        public double? RatingKinopoisk { get; init; }

        /// <summary>возрастные ограничения.</summary>
        [JsonPropertyName("ratingAgeLimits")]
        public string? RatingAgeLimits { get; init; }
    }
}