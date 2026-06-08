// tools/moviemaintenance.cs вЂ” СѓС‚РёР»РёС‚Р° РґР»СЏ РѕС‡РёСЃС‚РєРё Рё Р·Р°РіСЂСѓР·РєРё С„РёР»СЊРјРѕРІ
// Р·Р°РїСѓСЃРє: dotnet run -- --maintenance
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;
using MovieApp.Services.Kinopoisk;

namespace MovieApp.Tools
{
    public static class MovieMaintenance
    {
        private const string ApiKey = "241e5a50-f74e-4918-a5b3-ff163058e206";
        private const string BaseUrl = "https://kinopoiskapiunofficial.tech";
        private static readonly string LogFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "maintenance_log.txt");
        private static StreamWriter? _logWriter;

        private static void Log(string msg)
        {
            _logWriter?.WriteLine(msg);
            _logWriter?.Flush();
        }

        public static async Task RunAsync()
        {
            _logWriter = new StreamWriter(LogFile, false, System.Text.Encoding.UTF8);
            Log("=== УТИЛИТА ОБСЛУЖИВАНИЯ БАЗЫ ДАННЫХ ===");
            Log($"Старт: {DateTime.Now}");

            using (var db = new ApplicationDbContext())
            {
                Log("Сохраняем старую базу фильмов...");
                // try { db.Database.ExecuteSqlRaw("DELETE FROM \"MovieGenres\""); } catch { }
                // try { db.Database.ExecuteSqlRaw("DELETE FROM \"Ratings\""); } catch { }
                // try { db.Database.ExecuteSqlRaw("DELETE FROM \"Movies\""); } catch { }
                // await db.SaveChangesAsync();
            }

            Log("Загрузка 450 полностью валидных фильмов...");
            int loaded = await LoadNewMoviesAsync(450);
            Log($"  Загружено: {loaded} новых фильмов");

            Log("Загрузка 100 полностью валидных сериалов...");
            int loadedTv = await LoadNewTvSeriesAsync(100);
            Log($"  Загружено: {loadedTv} новых сериалов");

            // ─────── ИТОГ ───────
            using var dbFinal = new ApplicationDbContext();
            int totalMovies = dbFinal.Movies.Count();
            Log($"=== ГОТОВО! Фильмов в базе: {totalMovies} ===");
            Log($"Завешено: {DateTime.Now}");
            _logWriter?.Close();
        }

        /// <summary>РЈРґР°Р»СЏРµС‚ С„РёР»СЊРјС‹ СЃ РїСѓСЃС‚С‹Рј/null/placeholder PosterUrl.</summary>
        private static async Task<int> DeleteMoviesWithoutPostersAsync()
        {
            using var db = new ApplicationDbContext();
            var badMovies = db.Movies.Where(m =>
                m.PosterUrl == null ||
                m.PosterUrl == "" ||
                m.PosterUrl.Contains("null") ||
                m.PosterUrl.Contains("default")).ToList();

            if (badMovies.Count == 0)
            {
                Log("  РќРµС‚ С„РёР»СЊРјРѕРІ Р±РµР· РїРѕСЃС‚РµСЂРѕРІ.");
                return 0;
            }

            foreach (var m in badMovies)
            {
                // СѓРґР°Р»СЏРµРј СЃРІСЏР·Р°РЅРЅС‹Рµ РґР°РЅРЅС‹Рµ
                var movieGenres = db.MovieGenres.Where(mg => mg.MovieId == m.Id);
                db.MovieGenres.RemoveRange(movieGenres);
                var ratings = db.Ratings.Where(r => r.MovieId == m.Id);
                db.Ratings.RemoveRange(ratings);
            }
            db.Movies.RemoveRange(badMovies);
            await db.SaveChangesAsync();

            Log($"  РЈРґР°Р»РµРЅРѕ С„РёР»СЊРјРѕРІ Р±РµР· РїРѕСЃС‚РµСЂРѕРІ: {badMovies.Count}");
            return badMovies.Count;
        }

        /// <summary>РџСЂРѕРІРµСЂСЏРµС‚ HTTP РґРѕСЃС‚СѓРїРЅРѕСЃС‚СЊ РєР°Р¶РґРѕРіРѕ РїРѕСЃС‚РµСЂР° Рё СѓРґР°Р»СЏРµС‚ С„РёР»СЊРјС‹ СЃ РЅРµРґРѕСЃС‚СѓРїРЅС‹РјРё.</summary>
        private static async Task<int> DeleteMoviesWithBrokenPostersAsync()
        {
            using var db = new ApplicationDbContext();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Add("User-Agent", "MovieApp/1.0");

            var allMovies = db.Movies.ToList();
            var brokenIds = new List<int>();
            int checked_ = 0;

            foreach (var movie in allMovies)
            {
                checked_++;
                if (checked_ % 20 == 0)
                    Log($"    РџСЂРѕРІРµСЂРµРЅРѕ {checked_}/{allMovies.Count}...");

                if (string.IsNullOrWhiteSpace(movie.PosterUrl))
                {
                    brokenIds.Add(movie.Id);
                    continue;
                }

                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, movie.PosterUrl);
                    using var resp = await http.SendAsync(req);
                    if (!resp.IsSuccessStatusCode)
                    {
                        // РїРѕРїСЂРѕР±СѓРµРј GET РЅР° СЃР»СѓС‡Р°Р№ РµСЃР»Рё HEAD РЅРµ РїРѕРґРґРµСЂР¶РёРІР°РµС‚СЃСЏ
                        using var getResp = await http.GetAsync(movie.PosterUrl, HttpCompletionOption.ResponseHeadersRead);
                        if (!getResp.IsSuccessStatusCode)
                        {
                            Log($"    вњ— ID {movie.Id} \"{movie.Title}\" вЂ” РїРѕСЃС‚РµСЂ РЅРµРґРѕСЃС‚СѓРїРµРЅ ({(int)getResp.StatusCode})");
                            brokenIds.Add(movie.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"    вњ— ID {movie.Id} \"{movie.Title}\" вЂ” РѕС€РёР±РєР°: {ex.Message.Substring(0, Math.Min(60, ex.Message.Length))}");
                    brokenIds.Add(movie.Id);
                }

                await Task.Delay(50); // РјСЏРіРєРёР№ rate-limit
            }

            if (brokenIds.Count == 0)
            {
                Log("  Р’СЃРµ РїРѕСЃС‚РµСЂС‹ РґРѕСЃС‚СѓРїРЅС‹!");
                return 0;
            }

            // СѓРґР°Р»СЏРµРј
            foreach (var id in brokenIds)
            {
                var mg = db.MovieGenres.Where(x => x.MovieId == id);
                db.MovieGenres.RemoveRange(mg);
                var rt = db.Ratings.Where(x => x.MovieId == id);
                db.Ratings.RemoveRange(rt);
                var m = db.Movies.Find(id);
                if (m != null) db.Movies.Remove(m);
            }
            await db.SaveChangesAsync();

            Log($"  РЈРґР°Р»РµРЅРѕ СЃ Р±РёС‚С‹РјРё РїРѕСЃС‚РµСЂР°РјРё: {brokenIds.Count}");
            return brokenIds.Count;
        }

        /// <summary>Р—Р°РіСЂСѓР¶Р°РµС‚ РЅРѕРІС‹Рµ С„РёР»СЊРјС‹ РёР· РљРёРЅРѕРїРѕРёСЃРєР° (СЃ РїСЂРѕРІРµСЂРєРѕР№ РїРѕСЃС‚РµСЂР° РїРµСЂРµРґ РґРѕР±Р°РІР»РµРЅРёРµРј).</summary>
        private static async Task<int> LoadNewMoviesAsync(int targetCount)
        {
            using var db = new ApplicationDbContext();
            using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            http.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.Add("X-API-KEY", ApiKey);

            using var posterCheck = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            posterCheck.DefaultRequestHeaders.Add("User-Agent", "MovieApp/1.0");

            var existingIds = (await db.Movies.Select(m => m.Id).ToListAsync()).ToHashSet();
            var existingGenres = await db.Genres.ToDictionaryAsync(g => g.Name, g => g);

            int needed = targetCount - existingIds.Count;
            if (needed <= 0)
            {
                Log($"База уже содержит {existingIds.Count} фильмов (цель: {targetCount}).");
                return 0;
            }

            int added = 0;
            int page = 1;
            int maxPages = 30;

            // РєРѕР»Р»РµРєС†РёРё РґР»СЏ РїРµСЂРµР±РѕСЂР° РµСЃР»Рё РѕРґРЅР° РёСЃС‡РµСЂРїР°Р»Р°СЃСЊ
            string[] collections = { "TOP_250_MOVIES", "TOP_POPULAR_MOVIES", "TOP_POPULAR_ALL", "TOP_250_TV_SHOWS" };
            int collectionIdx = 0;

            while (added < needed && collectionIdx < collections.Length)
            {
                string collection = collections[collectionIdx];
                var url = $"/api/v2.2/films/collections?type={collection}&page={page}";

                Log($"    Р—Р°РїСЂРѕСЃ: {collection} page={page}...");

                KinopoiskCollectionResponse? data;
                try
                {
                    using var resp = await http.GetAsync(url);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Log($"    РћС€РёР±РєР° API: {resp.StatusCode}");
                        if ((int)resp.StatusCode == 402) break; // Р»РёРјРёС‚ API
                        page++;
                        if (page > maxPages) { page = 1; collectionIdx++; }
                        continue;
                    }
                    data = await resp.Content.ReadFromJsonAsync<KinopoiskCollectionResponse>();
                }
                catch (Exception ex)
                {
                    Log($"    РћС€РёР±РєР°: {ex.Message}");
                    break;
                }

                if (data?.Items == null || data.Items.Count == 0)
                {
                    page = 1; collectionIdx++;
                    continue;
                }

                foreach (var film in data.Items)
                {
                    if (added >= targetCount) break;

                    if (existingIds.Contains(film.KinopoiskId)) continue;

                    // РїСЂРѕРїСѓСЃРєР°РµРј Р±РµР· РїРѕСЃС‚РµСЂР°
                    if (string.IsNullOrWhiteSpace(film.PosterUrl) ||
                        film.PosterUrl.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                        film.PosterUrl.Contains("default", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // РїСЂРѕРїСѓСЃРєР°РµРј С„РёР»СЊРјС‹ РґР»СЏ РІР·СЂРѕСЃР»С‹С…
                    if (film.Genres.Any(g => g.Genre?.Equals("РґР»СЏ РІР·СЂРѕСЃР»С‹С…", StringComparison.OrdinalIgnoreCase) == true))
                        continue;

                    // РїСЂРѕРІРµСЂСЏРµРј РґРѕСЃС‚СѓРїРЅРѕСЃС‚СЊ РїРѕСЃС‚РµСЂР°
                    bool posterOk = false;
                    try
                    {
                        using var pResp = await posterCheck.GetAsync(film.PosterUrl, HttpCompletionOption.ResponseHeadersRead);
                        posterOk = pResp.IsSuccessStatusCode;
                    }
                    catch { }

                    if (!posterOk)
                    {
                        Log($"    РџСЂРѕРїСѓСЃРє ID {film.KinopoiskId} вЂ” РїРѕСЃС‚РµСЂ РЅРµРґРѕСЃС‚СѓРїРµРЅ");
                        continue;
                    }

                    // РїРѕР»СѓС‡Р°РµРј РґРµС‚Р°Р»Рё
                    var movie = new Movie
                    {
                        Id = film.KinopoiskId,
                        Title = film.NameRu ?? $"Р¤РёР»СЊРј #{film.KinopoiskId}",
                        Year = film.Year,
                        PosterUrl = film.PosterUrl,
                        Type = film.Type
                    };

                    bool isValid = false;
                    try
                    {
                        using var detResp = await http.GetAsync($"/api/v2.2/films/{film.KinopoiskId}");
                        if (detResp.IsSuccessStatusCode)
                        {
                            var det = await detResp.Content.ReadFromJsonAsync<KinopoiskFilmDetailResponse>();
                            if (det != null)
                            {
                                movie.Description = det.Description;
                                movie.RatingKinopoisk = det.RatingKinopoisk;
                                movie.AgeRating = det.RatingAgeLimits != null 
                                    ? (det.RatingAgeLimits.Replace("age", "").Replace("+", "") + "+")
                                    : null;

                                // Strict validation: Description, PosterUrl, AgeRating/RatingAgeLimits, and Score/RatingKinopoisk
                                if (!string.IsNullOrWhiteSpace(movie.Description) &&
                                    !string.IsNullOrWhiteSpace(movie.PosterUrl) &&
                                    !string.IsNullOrWhiteSpace(det.RatingAgeLimits) &&
                                    movie.RatingKinopoisk != null &&
                                    movie.RatingKinopoisk > 0)
                                {
                                    isValid = true;
                                }
                            }
                        }
                        await Task.Delay(200);
                    }
                    catch { }

                    if (!isValid)
                    {
                        Log($"    Пропуск ID {film.KinopoiskId} — отсутствуют обязательные поля (описание/постер/рейтинг/возраст)");
                        continue;
                    }

                    db.Movies.Add(movie);
                    existingIds.Add(film.KinopoiskId);

                    // Р¶Р°РЅСЂС‹
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
                    added++;

                    if (added % 10 == 0)
                        Log($"    Р—Р°РіСЂСѓР¶РµРЅРѕ {added}/{targetCount}...");
                }

                if (page >= (data.TotalPages > maxPages ? maxPages : data.TotalPages))
                {
                    page = 1; collectionIdx++;
                }
                else
                {
                    page++;
                }
            }

            return added;
        }

        private static async Task<int> LoadNewTvSeriesAsync(int targetCount)
        {
            using var db = new ApplicationDbContext();
            using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            http.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.Add("X-API-KEY", ApiKey);

            using var posterCheck = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            posterCheck.DefaultRequestHeaders.Add("User-Agent", "MovieApp/1.0");

            var existingIds = (await db.Movies.Select(m => m.Id).ToListAsync()).ToHashSet();
            var existingGenres = await db.Genres.ToDictionaryAsync(g => g.Name, g => g);

            // Count existing TV Series
            int existingTvCount = await db.Movies.CountAsync(m => m.Type == "TV_SERIES");
            int needed = targetCount - existingTvCount;
            if (needed <= 0)
            {
                Log($"База уже содержит {existingTvCount} сериалов (цель: {targetCount}).");
                return 0;
            }

            int added = 0;
            int page = 1;
            int maxPages = 30;

            while (added < needed)
            {
                var url = $"/api/v2.2/films?type=TV_SERIES&page={page}";

                Log($"    Запрос сериалов page={page}...");

                KinopoiskCollectionResponse? data;
                try
                {
                    using var resp = await http.GetAsync(url);
                    if (!resp.IsSuccessStatusCode)
                    {
                        Log($"    Ошибка API сериалов: {resp.StatusCode}");
                        if ((int)resp.StatusCode == 402) break; // лимит API
                        page++;
                        if (page > maxPages) break;
                        continue;
                    }
                    data = await resp.Content.ReadFromJsonAsync<KinopoiskCollectionResponse>();
                }
                catch (Exception ex)
                {
                    Log($"    Ошибка API сериалов: {ex.Message}");
                    break;
                }

                if (data?.Items == null || data.Items.Count == 0)
                {
                    break;
                }

                foreach (var film in data.Items)
                {
                    if (added >= needed) break;

                    if (existingIds.Contains(film.KinopoiskId)) continue;

                    // пропускаем без постера
                    if (string.IsNullOrWhiteSpace(film.PosterUrl) ||
                        film.PosterUrl.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                        film.PosterUrl.Contains("default", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // пропускаем фильмы для взрослых
                    if (film.Genres.Any(g => g.Genre?.Equals("для взрослых", StringComparison.OrdinalIgnoreCase) == true))
                        continue;

                    // проверяем доступность постера
                    bool posterOk = false;
                    try
                    {
                        using var pResp = await posterCheck.GetAsync(film.PosterUrl, HttpCompletionOption.ResponseHeadersRead);
                        posterOk = pResp.IsSuccessStatusCode;
                    }
                    catch { }

                    if (!posterOk)
                    {
                        Log($"    Пропуск сериала ID {film.KinopoiskId} — постер недоступен");
                        continue;
                    }

                    // получаем детали
                    var movie = new Movie
                    {
                        Id = film.KinopoiskId,
                        Title = film.NameRu ?? $"Сериал #{film.KinopoiskId}",
                        Year = film.Year,
                        PosterUrl = film.PosterUrl,
                        Type = "TV_SERIES"
                    };

                    bool isValid = false;
                    try
                    {
                        using var detResp = await http.GetAsync($"/api/v2.2/films/{film.KinopoiskId}");
                        if (detResp.IsSuccessStatusCode)
                        {
                            var det = await detResp.Content.ReadFromJsonAsync<KinopoiskFilmDetailResponse>();
                            if (det != null)
                            {
                                movie.Description = det.Description;
                                movie.RatingKinopoisk = det.RatingKinopoisk;
                                movie.AgeRating = det.RatingAgeLimits != null 
                                    ? (det.RatingAgeLimits.Replace("age", "").Replace("+", "") + "+")
                                    : null;

                                // Strict validation: Description, PosterUrl, AgeRating/RatingAgeLimits, and Score/RatingKinopoisk
                                if (!string.IsNullOrWhiteSpace(movie.Description) &&
                                    !string.IsNullOrWhiteSpace(movie.PosterUrl) &&
                                    !string.IsNullOrWhiteSpace(det.RatingAgeLimits) &&
                                    movie.RatingKinopoisk != null &&
                                    movie.RatingKinopoisk > 0)
                                {
                                    isValid = true;
                                }
                            }
                        }
                        await Task.Delay(200);
                    }
                    catch { }

                    if (!isValid)
                    {
                        Log($"    Пропуск сериала ID {film.KinopoiskId} — отсутствуют обязательные поля (описание/постер/рейтинг/возраст)");
                        continue;
                    }

                    db.Movies.Add(movie);
                    existingIds.Add(film.KinopoiskId);

                    // жанры
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
                    added++;

                    if (added % 10 == 0)
                        Log($"    Загружено сериалов {added}/{needed}...");
                }

                if (page >= (data.TotalPages > maxPages ? maxPages : data.TotalPages))
                {
                    break;
                }
                else
                {
                    page++;
                }
            }

            return added;
        }

        public static async Task FinalSeedDatabaseAsync()
        {
            System.Console.WriteLine("=== СТАРТ ПРОТОКОЛА RESET & SEED ===");
            
            using (var db = new ApplicationDbContext())
            {
                System.Console.WriteLine("Шаг 1: Очистка таблиц в строгом порядке PostgreSQL...");
                
                // Удаляем сообщения чата поддержки
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SupportMessages\"");
                    System.Console.WriteLine("  - Таблица SupportMessages очищена.");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки SupportMessages: {ex.Message}"); }

                // Удаляем тикеты поддержки
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"SupportTickets\"");
                    System.Console.WriteLine("  - Таблица SupportTickets очищена.");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки SupportTickets: {ex.Message}"); }

                // Удаляем комментарии
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Comments\"");
                    System.Console.WriteLine("  - Таблица Comments очищена.");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки Comments: {ex.Message}"); }

                // Удаляем оценки
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Ratings\"");
                    System.Console.WriteLine("  - Таблица Ratings очищена.");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки Ratings: {ex.Message}"); }

                // Удаляем уведомления
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Notifications\"");
                    System.Console.WriteLine("  - Таблица Notifications очищена.");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки Notifications: {ex.Message}"); }

                // Удаляем дружеские связи
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Friendships\"");
                    System.Console.WriteLine("  - Таблица Friendships очищена.");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки Friendships: {ex.Message}"); }

                // Удаляем пользователей кроме admin
                try
                {
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Login\" <> 'admin'");
                    System.Console.WriteLine("  - Таблица Users очищена (кроме admin).");
                }
                catch (Exception ex) { System.Console.WriteLine($"  [!] Ошибка очистки Users: {ex.Message}"); }

                await db.SaveChangesAsync();
                System.Console.WriteLine("Очистка успешно завершена.");
            }

            System.Console.WriteLine("\nШаг 2: Генерация 23 новых учетных записей...");
            
            var userLogins = new List<string>
            {
                "Иван", "Алексей", "Киноман99", "Светлана", "Дмитрий",
                "Мария", "Елена", "Сергей", "Ольга", "Андрей",
                "Артем", "Наталья", "Михаил", "Анна", "Егор",
                "Татьяна", "Роман", "Юлия", "Николай", "Екатерина",
                "Модератор1", "Модератор2",
                "СуперАдмин"
            };

            var userRoles = new List<string>();
            for (int i = 0; i < 20; i++) userRoles.Add("User");
            userRoles.Add("Moderator");
            userRoles.Add("Moderator");
            userRoles.Add("Admin");

            var seededUsers = new List<User>();

            using (var db = new ApplicationDbContext())
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                
                for (int i = 0; i < 23; i++)
                {
                    string login = userLogins[i];
                    string role = userRoles[i];
                    string password = role == "User" ? "123456" : (role == "Moderator" ? "mod123" : "admin123");

                    string? avatarBase64 = null;
                    
                    // Correction 1: Cache-Busting for Picsum Avatars
                    string avatarUrl = $"https://picsum.photos/150?random={Guid.NewGuid()}";
                    System.Console.WriteLine($"Скачивание аватара для пользователя {login} ({i + 1}/23)...");

                    try
                    {
                        byte[] imageBytes = await http.GetByteArrayAsync(avatarUrl);
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            byte[] compressed = CompressBytesToJpeg(imageBytes, 150, 150, 75);
                            avatarBase64 = Convert.ToBase64String(compressed);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"  [!] Не удалось скачать аватар для {login}: {ex.Message}. Используем пустой аватар.");
                    }

                    var newUser = new User
                    {
                        Login = login,
                        Role = role,
                        Password = password,
                        AvatarBase64 = avatarBase64,
                        LastActivity = DateTime.UtcNow
                    };

                    db.Users.Add(newUser);
                    seededUsers.Add(newUser);
                }

                await db.SaveChangesAsync();
                System.Console.WriteLine($"Успешно сохранено 23 учетных записей. Получаем их сгенерированные ID...");
            }

            // Получим свежий список созданных пользователей из БД, чтобы ID были валидными
            using (var db = new ApplicationDbContext())
            {
                var dbSeededUsers = await db.Users.Where(u => u.Login != "admin").ToListAsync();
                
                System.Console.WriteLine("\nШаг 3: Генерация случайных оценок и комментариев для фильмов...");
                
                var positiveReviews = new[]
                {
                    "Отличный фильм, очень рекомендую!",
                    "Шедевр своего времени!",
                    "Замечательная актерская игра и сюжет.",
                    "Прекрасный фильм для вечернего просмотра.",
                    "Просто потрясающе, 10 из 10!",
                    "Очень сильное кино, оставляет послевкусие.",
                    "Великолепная режиссура и саундтрек.",
                    "Один из лучших фильмов, что я видел!"
                };

                var neutralReviews = new[]
                {
                    "Неплохо, но на один раз.",
                    "Средний фильм, ожидал большего.",
                    "Интересная задумка, но слабая реализация.",
                    "Спецэффекты хорошие, а сюжет подкачал.",
                    "Нормальный фильм для отдыха.",
                    "Местами затянуто, но в целом смотреть можно.",
                    "Актеры старались, но сценарий подкачал.",
                    "Обычное кино, ничего выдающегося."
                };

                var negativeReviews = new[]
                {
                    "Полное разочарование.",
                    "Ужасно скучно, заснул на середине.",
                    "Плохая игра актеров и глупый сценарий.",
                    "Зря потраченное время.",
                    "Мне совсем не понравилось, не советую.",
                    "Фильм ни о чем, бессмысленный сюжет.",
                    "Жаль потраченных денег и времени.",
                    "Отвратительное зрелище."
                };

                var rand = new Random();
                var movies = await db.Movies.Take(80).ToListAsync(); // возьмем первые 80 фильмов

                int ratingsCount = 0;
                foreach (var movie in movies)
                {
                    // Для каждого фильма выбираем случайное количество пользователей от 1 до 7
                    int pickCount = rand.Next(1, 8);
                    
                    // Перемешиваем пользователей и берем нужное количество
                    var selectedUsers = dbSeededUsers.OrderBy(u => rand.Next()).Take(pickCount).ToList();

                    foreach (var user in selectedUsers)
                    {
                        int score = rand.Next(1, 6); // от 1 до 5
                        
                        string commentText;
                        if (score >= 4)
                            commentText = positiveReviews[rand.Next(positiveReviews.Length)];
                        else if (score == 3)
                            commentText = neutralReviews[rand.Next(neutralReviews.Length)];
                        else
                            commentText = negativeReviews[rand.Next(negativeReviews.Length)];

                        var rating = new Rating
                        {
                            UserId = user.Id,
                            MovieId = movie.Id,
                            Score = score,
                            PersonalNote = "Заметка добавлена при автозаполнении."
                        };
                        db.Ratings.Add(rating);

                        var comment = new Comment
                        {
                            UserId = user.Id,
                            MovieId = movie.Id,
                            Text = commentText,
                            CreatedAt = DateTime.UtcNow.AddMinutes(-rand.Next(10, 10000))
                        };
                        db.Comments.Add(comment);
                        
                        ratingsCount++;
                    }
                }

                await db.SaveChangesAsync();
                System.Console.WriteLine($"Успешно создано {ratingsCount} оценок и соответствующих комментариев.");

                System.Console.WriteLine("\nШаг 4: Генерация дружеских связей между пользователями...");
                var existingPairs = new HashSet<(int, int)>();
                int friendshipsCount = 0;

                for (int i = 0; i < dbSeededUsers.Count; i++)
                {
                    var user = dbSeededUsers[i];
                    int friendsCount = rand.Next(2, 6); // от 2 до 5 друзей
                    
                    for (int k = 0; k < friendsCount; k++)
                    {
                        var friend = dbSeededUsers[rand.Next(dbSeededUsers.Count)];
                        if (user.Id == friend.Id) continue;

                        int minId = Math.Min(user.Id, friend.Id);
                        int maxId = Math.Max(user.Id, friend.Id);
                        if (existingPairs.Contains((minId, maxId))) continue;

                        existingPairs.Add((minId, maxId));

                        var friendship = new Friendship
                        {
                            UserId = user.Id,
                            FriendId = friend.Id,
                            Status = "Accepted"
                        };
                        db.Friendships.Add(friendship);
                        friendshipsCount++;
                    }
                }

                await db.SaveChangesAsync();
                System.Console.WriteLine($"Успешно создано {friendshipsCount} дружеских связей (в статусе Accepted).");
            }

            System.Console.WriteLine("\n=== ПРОТОКОЛ RESET & SEED УСПЕШНО ЗАВЕРШЕН ===");
        }

        private static byte[] CompressBytesToJpeg(byte[] bytes, int maxWidth, int maxHeight, int quality)
        {
            var original = new System.Windows.Media.Imaging.BitmapImage();
            using (var stream = new System.IO.MemoryStream(bytes))
            {
                original.BeginInit();
                original.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                original.StreamSource = stream;
                original.EndInit();
            }

            double width = original.PixelWidth;
            double height = original.PixelHeight;
            double scale = 1.0;

            if (width > maxWidth || height > maxHeight)
            {
                double scaleW = (double)maxWidth / width;
                double scaleH = (double)maxHeight / height;
                scale = Math.Min(scaleW, scaleH);
            }

            var resized = new System.Windows.Media.Imaging.TransformedBitmap(original, new System.Windows.Media.ScaleTransform(scale, scale));
            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(resized));

            using var ms = new System.IO.MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
