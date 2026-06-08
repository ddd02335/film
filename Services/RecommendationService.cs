// services/recommendationservice.cs
// алгоритм: взвешенная контентная фильтрация на основе жанрового профиля пользователя.
// используется для персональных рекомендаций на странице «рекомендации».
// этапы работы:
// 1. загрузка оценок пользователя с жанрами оценённых фильмов.
// 2. холодный старт: если оценок < 3 возвращаем топ по рейтингу кинопоиска.
// 3. построение жанрового профиля (dictionary<string, double>):
// оценка 5 → +2.0, оценка 4 → +1.0, оценка 3 → 0, оценка 2 → -1.0, оценка 1 → -2.0.
// 4. нормализация весов (деление на максимальный вес) защита от перекоса при большом числе оценок.
// 5. скоринг кандидатов:
// matchscore = σ(вес жанра) + бонус качества (ratingkinopoisk × 0.5).
// 6. фильтрация: отбрасываем фильмы с matchscore ≤ 0.
// 7. сортировка по убыванию matchscore, take(limit).

using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Services
{
    /// <summary> сервис персональных рекомендаций фильмов. реализует алгоритм взвешенной контентной фильтрации (weighted content-based filtering). </summary>
    public class RecommendationService
    {
        private readonly ApplicationDbContext _db;

        // минимальное число оценок для запуска персонального алгоритма
        private const int ColdStartThreshold = 3;

        // жанры, скрываемые при включённом родительском контроле
        private static readonly HashSet<string> MatureGenres = new(StringComparer.OrdinalIgnoreCase)
        {
            "Ужасы", "Триллер", "Криминал", "Эротика"
        };

        public RecommendationService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary> возвращает персональные рекомендации для пользователя. при недостаточном числе оценок (холодный старт) топ по рейтингу кинопоиска. </summary> <param name="userid">идентификатор пользователя.</param> <param name="limit">максимальное число возвращаемых фильмов.</param>
        public async Task<List<Movie>> GetRecommendationsAsync(int userId, int limit = 20)
        {
            // проверяем состояние родительского контроля
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            bool parentalControl = user?.IsParentalControlEnabled ?? false;

            // загружаем оценки вместе с именами жанров оценённых фильмов.
            var dbRatings = await _db.Ratings
                .AsNoTracking()
                .Include(r => r.Movie)
                    .ThenInclude(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            var userRatings = dbRatings.Select(r => new
            {
                r.MovieId,
                r.Score,
                // загружаем имена жанров в памяти
                GenreNames = r.Movie != null
                    ? r.Movie.MovieGenres.Select(mg => mg.Genre.Name).ToList()
                    : new List<string>()
            }).ToList();

            // множество id уже оценённых фильмов (исключим их из кандидатов)
            var ratedMovieIds = userRatings.Select(r => r.MovieId).ToHashSet();

            // ── шаг 2: холодный старт ─────────────────────────────────────────────────
            // если пользователь оценил меньше coldstartthreshold фильмов,
            // жанровый профиль недостаточно репрезентативен.
            // возвращаем топ фильмов по рейтингу кинопоиска из ещё не оценённых.
            if (userRatings.Count < ColdStartThreshold)
            {
                var coldStartMovies = await _db.Movies
                    .AsNoTracking()
                    .Include(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                    .Where(m => !ratedMovieIds.Contains(m.Id))
                    .Where(m => m.RatingKinopoisk != null)
                    .OrderByDescending(m => m.RatingKinopoisk)
                    .ToListAsync();

                // фильтрация родительского контроля для холодного старта
                if (parentalControl)
                    coldStartMovies = ApplyParentalFilter(coldStartMovies);

                return coldStartMovies.Take(limit).ToList();
            }

            // ── шаг 3: построение жанрового профиля пользователя ─────────────────────
            // словарь genreweights: ключ = название жанра, значение = суммарный вес.
            // система очков:
            // оценка 5 → +2.0 (сильная симпатия)
            // оценка 4 → +1.0 (симпатия)
            // оценка 3 → 0.0 (нейтрально)
            // оценка 2 → -1.0 (антипатия)
            // оценка 1 → -2.0 (сильная антипатия)
            var genreWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var rating in userRatings)
            {
                double delta = rating.Score switch
                {
                    5 => +2.0,
                    4 => +1.0,
                    3 =>  0.0,
                    2 => -1.0,
                    1 => -2.0,
                    _ =>  0.0
                };

                if (delta == 0.0) continue;

                foreach (var genre in rating.GenreNames)
                {
                    if (!genreWeights.ContainsKey(genre))
                        genreWeights[genre] = 0.0;

                    genreWeights[genre] += delta;
                }
            }

            // ── шаг 4: нормализация жанровых весов ───────────────────────────────────
            // делим все веса на максимальный абсолютный вес.
            // приводит веса в диапазон [-1, +1] и устраняет перекос
            // при большом числе однотипных оценок.
            double maxAbsWeight = genreWeights.Values.Select(Math.Abs).DefaultIfEmpty(1.0).Max();
            if (maxAbsWeight > 0)
            {
                foreach (var key in genreWeights.Keys.ToList())
                    genreWeights[key] /= maxAbsWeight;
            }

            // запрашиваем только те фильмы, которые пользователь ещё не оценивал,
            // с жанрами для последующего скоринга.
            var candidates = await _db.Movies
                .AsNoTracking()
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Where(m => !ratedMovieIds.Contains(m.Id))
                .ToListAsync();

            // фильтрация родительского контроля для кандидатов
            if (parentalControl)
                candidates = ApplyParentalFilter(candidates);

            // ── шаг 6: вычисление matchscore для каждого кандидата ───────────────────
            // matchscore = (σ(нормализованный вес жанра кандидата) * 5.0)
            // + бонус качества (ratingkinopoisk × 0.1)
            // теперь веса жанров имеют решающее значение (множитель 5.0),
            // а рейтинг кинопоиска служит лишь небольшим бонусом (множитель 0.1).
            // Сначала вычисляем MatchScore полностью в памяти (In-Memory Calculation)
            var scoredWithScores = candidates
                .Select(movie =>
                {
                    // суммируем нормализованные веса жанров данного фильма
                    double genreScore = movie.MovieGenres
                        .Select(mg => mg.Genre.Name)
                        .Where(genreName => genreWeights.ContainsKey(genreName))
                        .Sum(genreName => genreWeights[genreName]);

                    // бонус качества от рейтинга кинопоиска (0..10 × 0.1 = 0..1)
                    double qualityBonus = (movie.RatingKinopoisk ?? 0.0) * 0.1;

                    return new
                    {
                        Movie      = movie,
                        MatchScore = (genreScore * 5.0) + qualityBonus
                    };
                })
                .OrderByDescending(x => x.MatchScore)
                .ToList();

            // Если список пуст, или у первого фильма MatchScore равен ровно 0, или все веса жанров равны 0,
            // запускаем Smart Fallback.
            bool triggerFallback = scoredWithScores.Count == 0 || 
                                   scoredWithScores[0].MatchScore == 0.0 || 
                                   genreWeights.Count == 0 || 
                                   genreWeights.Values.All(w => w == 0.0);

            if (triggerFallback)
            {
                // Запрашиваем из базы данных топ unrated фильмов (Smart Fallback)
                var fallbackQuery = _db.Movies
                    .AsNoTracking()
                    .Include(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                    .Where(m => !ratedMovieIds.Contains(m.Id))
                    .Where(m => m.RatingKinopoisk != null);

                List<Movie> fallbackResult;
                if (parentalControl)
                {
                    var allUnrated = await fallbackQuery.ToListAsync();
                    fallbackResult = ApplyParentalFilter(allUnrated)
                        .OrderByDescending(m => m.RatingKinopoisk)
                        .Take(limit)
                        .ToList();
                }
                else
                {
                    fallbackResult = await fallbackQuery
                        .OrderByDescending(m => m.RatingKinopoisk)
                        .Take(limit)
                        .ToListAsync();
                }
                return fallbackResult;
            }

            return scoredWithScores.Take(limit).Select(x => x.Movie).ToList();
        }

        /// <summary>Исключает зрелый контент: фильмы 18+ и зрелые жанры.</summary>
        private static List<Movie> ApplyParentalFilter(List<Movie> movies)
        {
            return movies.Where(m =>
            {
                // исключаем по возрастному рейтингу 18+
                if (!string.IsNullOrEmpty(m.AgeRating) && m.AgeRating.Contains("18"))
                    return false;

                // исключаем по зрелым жанрам
                bool hasMatureGenre = m.MovieGenres
                    .Any(mg => MatureGenres.Contains(mg.Genre.Name));
                return !hasMatureGenre;
            }).ToList();
        }

        public async Task<List<Movie>> GetFriendsPopularRecommendationsAsync(int userId, int limit = 20)
        {
            // 1. Проверяем родительский контроль
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            bool parentalControl = user?.IsParentalControlEnabled ?? false;

            // 2. Получаем ID принятых друзей (исключая удаленных)
            var friendIds = await _db.Friendships
                .AsNoTracking()
                .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == "Accepted")
                .Select(f => f.UserId == userId ? f.FriendId : f.UserId)
                .ToListAsync();

            var activeFriendIds = await _db.Users
                .AsNoTracking()
                .Where(u => friendIds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            if (activeFriendIds.Count == 0)
                return new List<Movie>();

            // 3. Получаем ID фильмов, оцененных текущим пользователем
            var ratedMovieIds = await _db.Ratings
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .Select(r => r.MovieId)
                .ToListAsync();
            var ratedMovieSet = ratedMovieIds.ToHashSet();

            // 4. Находим фильмы с высокими оценками (>= 4) от активных друзей, которые текущий пользователь не оценивал
            var popularRatings = await _db.Ratings
                .AsNoTracking()
                .Include(r => r.Movie)
                    .ThenInclude(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                .Include(r => r.User)
                .Where(r => activeFriendIds.Contains(r.UserId) && r.Score >= 4 && !r.User.IsDeleted)
                .ToListAsync();

            var candidates = popularRatings
                .Where(r => !ratedMovieSet.Contains(r.MovieId) && r.Movie != null)
                .GroupBy(r => r.MovieId)
                .Select(g => new
                {
                    MovieId = g.Key,
                    Movie = g.First().Movie,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Movie!.RatingKinopoisk)
                .Select(x => x.Movie!)
                .ToList();

            // 5. Фильтрация по родительскому контролю
            if (parentalControl)
            {
                candidates = ApplyParentalFilter(candidates);
            }

            return candidates.Take(limit).ToList();
        }
    }
}