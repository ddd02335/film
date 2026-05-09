// pages/recommendationspage.xaml.cs
// страница персональных рекомендаций.
// использует recommendationservice с алгоритмом взвешенной контентной фильтрации.
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;
using MovieApp.Services;

namespace MovieApp.Pages
{
    public partial class RecommendationsPage : Page
    {
        private readonly int _userId;

        // общий http-клиент для кэша постеров (пере-используется)
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public RecommendationsPage(int userId)
        {
            InitializeComponent();
            _userId = userId;
            
            // подписываемся на loaded, рекомендации пересчитывались
            // каждый раз при открытии страницы пользователем.
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            PanelLoading.Visibility   = Visibility.Visible;
            PanelNoRatings.Visibility = Visibility.Collapsed;
            TxtCount.Visibility       = Visibility.Collapsed;
            RecPanel.ItemsSource      = null;

            List<Movie> movies;
            bool isColdStart;

            await using (var db = new ApplicationDbContext())
            {
                int ratingCount = await db.Ratings
                    .AsNoTracking()
                    .CountAsync(r => r.UserId == _userId);

                isColdStart = ratingCount < 3;

                var service = new RecommendationService(db);
                movies = await service.GetRecommendationsAsync(_userId, limit: 20);
            }

            PanelLoading.Visibility = Visibility.Collapsed;

            if (movies.Count == 0)
            {
                PanelNoRatings.Visibility = Visibility.Visible;
                return;
            }

            // формируем viewmodel для привязки карточек
            var items = movies.Select(m => new RecMovieViewModel(m)).ToList();
            RecPanel.ItemsSource = items;

            // счётчик с пояснением режима
            TxtCount.Text = isColdStart
                ? $"Подобрано для вас: {items.Count} фильм(ов)  ·  режим: топ по рейтингу (добавьте оценки для персонализации)"
                : $"Подобрано для вас: {items.Count} фильм(ов)  ·  алгоритм: взвешенная контентная фильтрация";
            TxtCount.Visibility = Visibility.Visible;

            // загружаем постеры в фоне, не блокируя ui
            PosterCacheService.EnsureCacheDirectory();
            _ = Task.Run(() => PreloadPostersAsync(items));
        }


        private async Task PreloadPostersAsync(IReadOnlyList<RecMovieViewModel> items)
        {
            using var semaphore = new SemaphoreSlim(4);

            var tasks = items
                .Where(m => !string.IsNullOrEmpty(m.PosterUrl))
                .Select(async movie =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var localPath = await PosterCacheService.GetOrDownloadAsync(
                            _http, movie.Id, movie.PosterUrl!);

                        if (localPath != null)
                            await Dispatcher.InvokeAsync(() => movie.SetLocalPath(localPath));
                    }
                    catch { /* постер недоступен — оставляем заглушку 🎬 */ }
                    finally { semaphore.Release(); }
                });

            await Task.WhenAll(tasks);
        }

        // ── клик по карточке рекомендации ─────────────────────────────────────

        private async void OnMovieCardClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not int movieId) return;

            e.Handled = true;

            Movie? movie;
            List<string> genres;
            try
            {
                await using var db = new ApplicationDbContext();
                movie = await db.Movies
                    .AsNoTracking()
                    .Include(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                    .FirstOrDefaultAsync(m => m.Id == movieId);

                if (movie == null) return;

                genres = movie.MovieGenres
                    .Select(mg => mg.Genre.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Рекомендации] Ошибка загрузки деталей: {ex.Message}");
                return;
            }

            var detailsWindow = new MovieDetailsWindow(movie, genres, _userId)
            {
                Owner = Window.GetWindow(this)
            };
            detailsWindow.ShowDialog();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // viewmodel карточки рекомендации (с поддержкой локального кэша постеров)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary> viewmodel карточки фильма на странице рекомендаций. поддерживает inotifypropertychanged для асинхронного обновления постера. </summary>
    public sealed class RecMovieViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int     Id        { get; }
        public string  Title     { get; }
        public string? PosterUrl { get; }
        public int?    Year      { get; }

        private string? _localPosterPath;

        /// <summary>локальный путь к постеру из кэша; null постер ещё не загружен.</summary>
        public string? LocalPosterPath
        {
            get => _localPosterPath;
            private set
            {
                _localPosterPath = value;
                OnPropertyChanged(nameof(LocalPosterPath));
                OnPropertyChanged(nameof(HasNoPoster));
            }
        }

        /// <summary>true постер не загружен, отображается заглушка 🎬.</summary>
        public bool HasNoPoster => string.IsNullOrEmpty(LocalPosterPath);

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this,
                new System.ComponentModel.PropertyChangedEventArgs(name));

        public RecMovieViewModel(Movie m)
        {
            Id        = m.Id;
            Title     = m.Title;
            PosterUrl = string.IsNullOrEmpty(m.PosterUrl) ? null : m.PosterUrl;
            Year      = m.Year;
        }

        public void SetLocalPath(string path) => LocalPosterPath = path;
    }
}