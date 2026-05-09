// services/tmdb/tmdbapiclient.cs низкоуровневый http-клиент для работы с tmdb api
using System.Net.Http;
using System.Net.Http.Json;

namespace MovieApp.Services.Tmdb
{
    public class TmdbApiClient
    {
        private readonly HttpClient _http;

        // базовый url для получения постеров фильмов
        private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";
        private const string ApiBaseUrl   = "https://api.themoviedb.org/3";

        public TmdbApiClient()
        {
            _http = new HttpClient();
        }

        // получаем полный список жанров фильмов (статичный, обновляется редко)
        public async Task<List<TmdbGenreDto>> FetchGenresAsync(string apiKey)
        {
            var url = $"{ApiBaseUrl}/genre/movie/list?api_key={apiKey}&language=ru-RU";
            var response = await _http.GetFromJsonAsync<TmdbGenreListResponse>(url);
            return response?.Genres ?? new List<TmdbGenreDto>();
        }

        // получаем одну страницу популярных фильмов (до 20 результатов)
        public async Task<TmdbMovieListResponse?> FetchPopularMoviesAsync(string apiKey, int page = 1)
        {
            var url = $"{ApiBaseUrl}/movie/popular?api_key={apiKey}&language=ru-RU&page={page}";
            return await _http.GetFromJsonAsync<TmdbMovieListResponse>(url);
        }

        // формируем полный url постера из относительного пути tmdb
        // пример: "/abc.jpg" → "https://image.tmdb.org/t/p/w500/abc.jpg"
        public static string BuildPosterUrl(string? posterPath)
        {
            if (string.IsNullOrEmpty(posterPath)) return string.Empty;
            return $"{ImageBaseUrl}{posterPath}";
        }
    }
}