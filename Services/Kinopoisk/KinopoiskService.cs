using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Services.Kinopoisk
{
    /// <summary> сервис для загрузки популярных фильмов из кинопоиска и сохранения их в локальную бд. использует эндпоинт: get /api/v2.2/films/collections?type=top_popular_all&page={n} </summary>
    public sealed class KinopoiskService : IDisposable
    {
        private const string BaseUrl = "https://kinopoiskapiunofficial.tech";

        private readonly HttpClient _httpClient;

        public KinopoiskService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };
            // указываем серверу, что ожидаем ответ в формате json.
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary> настраивает заголовок x-api-key перед выполнением запросов. </summary>
        private void ConfigureApiKey(string apiKey)
        {
            _httpClient.DefaultRequestHeaders.Remove("X-API-KEY");
            _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        }

        /// <summary> загрузка фильмов из коллекции top_popular_all. возвращает (получено, добавлено, пропущено). </summary>
        public async Task<(int fetched, int added, int skipped)> ImportMoviesAsync(string apiKey, int targetMovieCount = 700)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("api-ключ пуст.", nameof(apiKey));

            ConfigureApiKey(apiKey);
            await using var db = new ApplicationDbContext();

            var existingMovieIds = (await db.Movies.Select(m => m.Id).ToListAsync()).ToHashSet();
            var existingGenres = await db.Genres.ToDictionaryAsync(g => g.Name, g => g);

            int page = 1;
            int fetchedCount = 0;
            int addedCount = 0;
            int skippedCount = 0;

            while (existingMovieIds.Count < targetMovieCount || page == 1)
            {
                var url = $"/api/v2.2/films/collections?type=TOP_POPULAR_ALL&page={page}";

                KinopoiskCollectionResponse? responseData;
                using (var response = await _httpClient.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync();
                        throw new Exception($"ошибка api: {response.StatusCode} ({(int)response.StatusCode}). {err}");
                    }
                    responseData = await response.Content.ReadFromJsonAsync<KinopoiskCollectionResponse>();
                }

                if (responseData?.Items == null || responseData.Items.Count == 0) break;
                fetchedCount += responseData.Items.Count;

                foreach (var film in responseData.Items)
                {
                    try
                    {
                        if (existingMovieIds.Contains(film.KinopoiskId))
                        {
                            skippedCount++;
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(film.PosterUrl) || 
                            film.PosterUrl.Contains("null", StringComparison.OrdinalIgnoreCase) || 
                            film.PosterUrl.Contains("default", StringComparison.OrdinalIgnoreCase))
                        {
                            skippedCount++;
                            continue;
                        }

                        if (film.Genres.Any(g => g.Genre?.Equals("для взрослых", StringComparison.OrdinalIgnoreCase) == true))
                        {
                            skippedCount++;
                            continue;
                        }

                        var movie = new Movie
                        {
                            Id        = film.KinopoiskId,
                            Title     = film.NameRu ?? $"фильм #{film.KinopoiskId}",
                            Year      = film.Year,
                            PosterUrl = film.PosterUrl,
                            Type      = film.Type
                        };

                        try
                        {
                            var detailUrl = $"/api/v2.2/films/{film.KinopoiskId}";
                            using var detResp = await _httpClient.GetAsync(detailUrl);
                            if (detResp.IsSuccessStatusCode)
                            {
                                var det = await detResp.Content.ReadFromJsonAsync<KinopoiskFilmDetailResponse>();
                                if (det != null)
                                {
                                    movie.Description = det.Description;
                                    movie.RatingKinopoisk = det.RatingKinopoisk;
                                    movie.AgeRating = (det.RatingAgeLimits?.ToString().Replace("age", "").Replace("+", "") ?? "12") + "+";
                                }
                            }
                            await Task.Delay(150);
                        }
                        catch { /* детали не критичны */ }

                        db.Movies.Add(movie);
                        existingMovieIds.Add(film.KinopoiskId);
                        addedCount++;

                        foreach (var gItem in film.Genres)
                        {
                            var gName = gItem.Genre?.Trim();
                            if (string.IsNullOrEmpty(gName)) continue;

                            if (!existingGenres.TryGetValue(gName, out var genre))
                            {
                                genre = new Genre { Name = gName };
                                db.Genres.Add(genre);
                                await db.SaveChangesAsync();
                                existingGenres[gName] = genre;
                            }
                            db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = genre.Id });
                        }
                        await db.SaveChangesAsync();

                        if (existingMovieIds.Count >= targetMovieCount) break;
                    }
                    catch
                    {
                        skippedCount++;
                        continue;
                    }
                }

                if (page >= responseData.TotalPages || existingMovieIds.Count >= targetMovieCount) break;
                page++;
            }
            return (fetchedCount, addedCount, skippedCount);
        }

        /// <summary>освобождает ресурсы httpclient.</summary>
        public void Dispose() => _httpClient.Dispose();
    }
}