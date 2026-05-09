// pages/watchedpage.xaml.cs список просмотренных и оценённых фильмов
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;

namespace MovieApp.Pages
{
    public partial class WatchedPage : Page
    {
        private readonly int _userId;
        private ObservableCollection<WatchedViewModel> _watchedItems = new();

        public WatchedPage(int userId)
        {
            InitializeComponent();
            _userId = userId;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            using var db = new ApplicationDbContext();

            var ratings = await db.Ratings
                .AsNoTracking()
                .Where(r => r.UserId == _userId)
                .Include(r => r.Movie)
                    .ThenInclude(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                .OrderByDescending(r => r.Score)
                .ToListAsync();

            var items = ratings.Select(r => new WatchedViewModel
            {
                Id         = r.MovieId,
                Title      = r.Movie.Title,
                PosterUrl  = string.IsNullOrEmpty(r.Movie.PosterUrl) ? null : r.Movie.PosterUrl,
                Year       = r.Movie.Year?.ToString() ?? "—",
                Genres     = string.Join(", ", r.Movie.MovieGenres.Select(mg => mg.Genre.Name)),
                Score      = r.Score,
                StarText   = new string('★', r.Score) + new string('☆', 5 - r.Score),
                ScoreLabel = $"{r.Score} / 5"
            }).ToList();

            _watchedItems = new ObservableCollection<WatchedViewModel>(items);
            WatchedList.ItemsSource = _watchedItems;
            TxtCount.Text = $"Всего оценено: {_watchedItems.Count} фильм(ов)";
            PanelEmpty.Visibility = _watchedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── удаление оценки ───────────────────────────────────────────────────
        
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int movieId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == _userId && r.MovieId == movieId);
                
                if (rating != null)
                {
                    db.Ratings.Remove(rating);
                    await db.SaveChangesAsync();

                    // удаляем из коллекции для мгновенного обновления ui
                    var itemToRemove = _watchedItems.FirstOrDefault(i => i.Id == movieId);
                    if (itemToRemove != null)
                    {
                        _watchedItems.Remove(itemToRemove);
                        TxtCount.Text = $"Всего оценено: {_watchedItems.Count} фильм(ов)";
                        PanelEmpty.Visibility = _watchedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Просмотренные] Ошибка удаления: {ex.Message}");
            }
        }
    }

    public class WatchedViewModel
    {
        public int     Id         { get; init; }
        public string  Title      { get; init; } = "";
        public string? PosterUrl  { get; init; }
        public string  Year       { get; init; } = "";
        public string  Genres     { get; init; } = "";
        public int     Score      { get; init; }
        public string  StarText   { get; init; } = "";
        public string  ScoreLabel { get; init; } = "";
    }
}