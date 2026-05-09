// services/tmdb/tmdbservice.cs сервис импорта фильмов из tmdb в локальную базу данных
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Services.Tmdb
{
    public class TmdbService
    {
        private readonly TmdbApiClient _api;
        private readonly ApplicationDbContext _db;

        public TmdbService(ApplicationDbContext db)
        {
            _db  = db;
            _api = new TmdbApiClient();
        }

        /// <summary> главная точка входа: загружает жанры и n страниц популярных фильмов, сохраняет всё в локальную базу данных sqlite. </summary>
        public async Task ImportMoviesAsync(string apiKey, int pagesToFetch = 1)
        {
            await ImportGenresAsync(apiKey);

            for (int page = 1; page <= pagesToFetch; page++)
            {
                await ImportMoviesPageAsync(apiKey, page);
            }
        }


        // загрузка и сохранение жанров (с проверкой на дубликаты)
        private async Task ImportGenresAsync(string apiKey)
        {
            var genres = await _api.FetchGenresAsync(apiKey);

            foreach (var dto in genres)
            {
                // пропускаем жанр, если он уже есть в базе данных
                bool exists = await _db.Genres.AnyAsync(g => g.Id == dto.Id);
                if (exists) continue;

                _db.Genres.Add(new Genre { Id = dto.Id, Name = dto.Name });
            }

            await _db.SaveChangesAsync();
        }

        private async Task ImportMoviesPageAsync(string apiKey, int page)
        {
            var response = await _api.FetchPopularMoviesAsync(apiKey, page);
            if (response == null) return;

            // загружаем все известные id жанров один раз для всей партии фильмов
            var knownGenreIds = (await _db.Genres
                .Select(g => g.Id)
                .ToListAsync()).ToHashSet();

            foreach (var dto in response.Results)
            {
                // пропускаем фильм, если он уже есть в базе данных
                bool movieExists = await _db.Movies.AnyAsync(m => m.Id == dto.Id);
                if (movieExists) continue;

                // безопасно парсим год из строки "2024-07-19"
                int? year = null;
                if (DateTime.TryParse(dto.ReleaseDate, out var date))
                    year = date.Year;

                var movie = new Movie
                {
                    Id          = dto.Id,
                    Title       = dto.Title,
                    Description = dto.Overview,
                    Year        = year,
                    PosterUrl   = TmdbApiClient.BuildPosterUrl(dto.PosterPath)
                };

                _db.Movies.Add(movie);

                // создаём связи фильм-жанр только для известных жанров
                foreach (var genreId in dto.GenreIds)
                {
                    if (!knownGenreIds.Contains(genreId)) continue;

                    _db.MovieGenres.Add(new MovieGenre
                    {
                        MovieId = dto.Id,
                        GenreId = genreId
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}