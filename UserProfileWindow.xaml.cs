using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp
{
    public partial class UserProfileWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private readonly int _targetUserId;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private string? _targetUserAvatarBase64;
        public string? TargetUserAvatarBase64
        {
            get => _targetUserAvatarBase64;
            set
            {
                _targetUserAvatarBase64 = value;
                OnPropertyChanged(nameof(TargetUserAvatarBase64));
                OnPropertyChanged(nameof(HasAvatar));
                OnPropertyChanged(nameof(HasNoAvatar));
            }
        }

        private string _targetUserInitials = "?";
        public string TargetUserInitials
        {
            get => _targetUserInitials;
            set
            {
                _targetUserInitials = value;
                OnPropertyChanged(nameof(TargetUserInitials));
            }
        }

        public bool HasAvatar => !string.IsNullOrEmpty(TargetUserAvatarBase64);
        public bool HasNoAvatar => string.IsNullOrEmpty(TargetUserAvatarBase64);

        public UserProfileWindow(int currentUserId, int targetUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            _targetUserId = targetUserId;
            DataContext = this;
            Loaded += UserProfileWindow_Loaded;
        }

        private async void UserProfileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserProfileAsync();
        }

        private async Task LoadUserProfileAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                // Фильтруем удаленных пользователей (IsDeleted == false)
                var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _targetUserId && !u.IsDeleted);
                if (targetUser == null)
                {
                    MessageBox.Show("Пользователь не найден или был удален.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Устанавливаем логин
                TxtLogin.Text = targetUser.Login;

                // Загружаем аватар
                TargetUserAvatarBase64 = targetUser.AvatarBase64;
                TargetUserInitials = string.IsNullOrEmpty(targetUser.Login) ? "?" : targetUser.Login.Substring(0, 1).ToUpper();

                // Проверяем роль текущего пользователя
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                bool isCurrentAdmin = currentUser?.Role != null && string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);

                // Проверяем приватность
                bool isHidden = false;
                if (targetUser.IsPrivate && _currentUserId != _targetUserId && !isCurrentAdmin)
                {
                    // Проверяем дружбу
                    bool areFriends = await db.Friendships.AnyAsync(f =>
                        ((f.UserId == _currentUserId && f.FriendId == _targetUserId) ||
                         (f.UserId == _targetUserId && f.FriendId == _currentUserId)) &&
                        f.Status == "Accepted");

                    if (!areFriends)
                    {
                        isHidden = true;
                    }
                }

                if (isHidden)
                {
                    StatsPanel.Visibility = Visibility.Collapsed;
                    MoviesPanel.Visibility = Visibility.Collapsed;
                    TxtHiddenProfile.Visibility = Visibility.Visible;

                    // Сбрасываем статистику
                    TxtWatchedCount.Text = "0";
                    TxtAverageScore.Text = "0.0";
                    TxtFavoriteGenre.Text = "—";
                    LvRatedMovies.ItemsSource = null;
                    TxtNoMovies.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StatsPanel.Visibility = Visibility.Visible;
                    MoviesPanel.Visibility = Visibility.Visible;
                    TxtHiddenProfile.Visibility = Visibility.Collapsed;

                    // Загружаем комментарии этого пользователя
                    var userComments = await db.Comments
                        .Include(c => c.Movie)
                        .Where(c => c.UserId == _targetUserId)
                        .OrderByDescending(c => c.CreatedAt)
                        .ToListAsync();

                    // Загружаем ВСЕ оценки этого пользователя для расчета статистики
                    var ratings = await db.Ratings
                        .Include(r => r.Movie)
                            .ThenInclude(m => m.MovieGenres)
                                .ThenInclude(mg => mg.Genre)
                        .Where(r => r.UserId == _targetUserId)
                        .ToListAsync();

                    // Считаем статистику по оцененным фильмам в памяти
                    int watchedCount = ratings.Count;
                    double avgScore = watchedCount > 0 ? ratings.Average(r => r.Score) : 0.0;

                    // Находим любимый жанр в памяти
                    var topGenre = ratings
                        .Where(r => r.Movie != null)
                        .SelectMany(r => r.Movie.MovieGenres ?? Enumerable.Empty<MovieGenre>())
                        .Where(mg => mg.Genre != null)
                        .GroupBy(mg => mg.Genre.Name)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault() ?? "—";

                    TxtWatchedCount.Text = watchedCount.ToString();
                    TxtAverageScore.Text = avgScore.ToString("0.0");
                    TxtFavoriteGenre.Text = topGenre;

                    // Формируем список для отображения на UI
                    var movieDisplayItems = userComments
                        .Select(c =>
                        {
                            var rating = ratings.FirstOrDefault(r => r.MovieId == c.MovieId);
                            return new UserProfileMovieItem
                            {
                                MovieId = c.MovieId,
                                Title = c.Movie?.Title ?? "Неизвестный фильм",
                                Year = c.Movie?.Year.HasValue == true ? $"({c.Movie.Year})" : string.Empty,
                                ScoreText = rating != null ? $"★ {rating.Score}/5" : "Нет оценки",
                                CommentText = c.Text,
                                CommentDateText = c.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                            };
                        })
                        .ToList();

                    LvRatedMovies.ItemsSource = movieDisplayItems;
                    TxtNoMovies.Visibility = movieDisplayItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки профиля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnDragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }

    public class UserProfileMovieItem
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string ScoreText { get; set; } = string.Empty;
        public string CommentText { get; set; } = string.Empty;
        public string CommentDateText { get; set; } = string.Empty;
    }
}
