using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;
using MovieApp.Pages;

namespace MovieApp
{
    public partial class MovieDetailsWindow : Window
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private readonly int _userId;
        private readonly int _movieId;
        private string _movieTitle = string.Empty;

        private List<CommentDisplayItem> _allComments = new();

        public MovieDetailsWindow(Movie movie, IEnumerable<string> genres, int userId)
        {
            InitializeComponent();
            _userId = userId;
            _movieId = movie.Id;
            _movieTitle = movie.Title;

            // Заполняем UI данными фильма
            TxtTitle.Text = movie.Title;
            TxtYear.Text = movie.Year.HasValue ? $"{movie.Year} г." : "Год не указан";
            TxtDescription.Text = string.IsNullOrWhiteSpace(movie.Description)
                ? "Описание отсутствует."
                : movie.Description;

            TxtKpRating.Text = movie.RatingKinopoisk.HasValue
                ? movie.RatingKinopoisk.Value.ToString("0.0")
                : "—";

            var genreList = genres.ToList();
            TxtGenres.Text = genreList.Count > 0
                ? string.Join("  ·  ", genreList)
                : "Жанры не указаны";

            TxtAgeRating.Text = movie.AgeRating ?? "12+";

            Loaded += async (_, _) =>
            {
                await LoadPosterAsync(movie);
                await InitializeCommentsAndRatingAsync();
            };
        }

        public MovieDetailsWindow(int movieId, int userId)
        {
            InitializeComponent();
            _userId = userId;
            _movieId = movieId;

            Loaded += async (_, _) =>
            {
                await LoadMovieDetailsAndInitAsync();
            };
        }

        private async Task LoadMovieDetailsAndInitAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var movie = await db.Movies
                    .Include(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                    .FirstOrDefaultAsync(m => m.Id == _movieId);

                if (movie != null)
                {
                    _movieTitle = movie.Title;
                    TxtTitle.Text = movie.Title;
                    TxtYear.Text = movie.Year.HasValue ? $"{movie.Year} г." : "Год не указан";
                    TxtDescription.Text = string.IsNullOrWhiteSpace(movie.Description)
                        ? "Описание отсутствует."
                        : movie.Description;

                    TxtKpRating.Text = movie.RatingKinopoisk.HasValue
                        ? movie.RatingKinopoisk.Value.ToString("0.0")
                        : "—";

                    var genresList = movie.MovieGenres.Select(mg => mg.Genre.Name).ToList();
                    TxtGenres.Text = genresList.Count > 0
                        ? string.Join("  ·  ", genresList)
                        : "Жанры не указаны";

                    TxtAgeRating.Text = movie.AgeRating ?? "12+";

                    await LoadPosterAsync(movie);
                }
                else
                {
                    _movieTitle = "Неизвестный фильм";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Детали] Ошибка загрузки деталей: {ex.Message}");
            }

            await InitializeCommentsAndRatingAsync();
        }

        private async Task InitializeCommentsAndRatingAsync()
        {
            await LoadCommentsAsync();
            await CheckCommentPermissionAsync();
        }

        private async Task CheckCommentPermissionAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var hasRated = await db.Ratings.AnyAsync(r => r.UserId == _userId && r.MovieId == _movieId);
                if (hasRated)
                {
                    CommentInputPanel.Visibility = Visibility.Visible;
                    TxtCommentWarning.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CommentInputPanel.Visibility = Visibility.Collapsed;
                    TxtCommentWarning.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Детали] Ошибка проверки прав комментирования: {ex.Message}");
            }
        }

        private async Task LoadCommentsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                // Фильтруем комментарии, исключая удаленных пользователей (IsDeleted == false)
                var comments = await db.Comments
                    .Include(c => c.User)
                    .Where(c => c.MovieId == _movieId && !c.User.IsDeleted)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                var userIds = comments.Select(c => c.UserId).Distinct().ToList();
                var ratings = await db.Ratings
                    .Where(r => r.MovieId == _movieId && userIds.Contains(r.UserId))
                    .ToDictionaryAsync(r => r.UserId, r => r.Score);

                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _userId);
                bool isCurrentAdmin = currentUser?.Role == "Admin";
                bool isCurrentMod = currentUser?.Role == "Moderator";
                bool isCurrentAdminOrMod = isCurrentAdmin || isCurrentMod;

                _allComments = comments.Select(c =>
                {
                    bool isPrivate = c.User?.IsPrivate ?? false;
                    bool hideInfo = isPrivate && !isCurrentAdmin;
                    return new CommentDisplayItem
                    {
                        Id = c.Id,
                        UserId = c.UserId,
                        UserLogin = hideInfo ? "Анонимный пользователь" : (c.User?.Login ?? "Удаленный пользователь"),
                        Text = c.Text,
                        CreatedAt = c.CreatedAt,
                        UserScore = ratings.ContainsKey(c.UserId) ? ratings[c.UserId] : 0,
                        RatingText = ratings.ContainsKey(c.UserId) ? $"★ {ratings[c.UserId]}/5" : string.Empty,
                        RatingVisibility = ratings.ContainsKey(c.UserId) ? Visibility.Visible : Visibility.Collapsed,
                        AvatarBase64 = hideInfo ? null : c.User?.AvatarBase64,
                        IsPrivate = hideInfo,
                        DeleteVisibility = (c.UserId == _userId || isCurrentAdminOrMod) ? Visibility.Visible : Visibility.Collapsed
                    };
                }).ToList();

                // Check for 1 comment constraint on load
                var myComment = _allComments.FirstOrDefault(c => c.UserId == _userId);
                if (myComment != null)
                {
                    TxtNewComment.Text = myComment.Text;
                    BtnSubmitComment.Content = "Сохранить";
                    BtnDeleteComment.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtNewComment.Text = string.Empty;
                    BtnSubmitComment.Content = "Отправить";
                    BtnDeleteComment.Visibility = Visibility.Collapsed;
                }

                ApplyCommentsSort();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки комментариев: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyCommentsSort()
        {
            if (_allComments == null || LvComments == null || CmbSortComments == null) return;

            IEnumerable<CommentDisplayItem> sorted = _allComments;

            int selectedIndex = CmbSortComments.SelectedIndex;
            if (selectedIndex == 0)
            {
                sorted = sorted.OrderByDescending(c => c.CreatedAt);
            }
            else if (selectedIndex == 1)
            {
                sorted = sorted.OrderBy(c => c.CreatedAt);
            }
            else if (selectedIndex == 2)
            {
                sorted = sorted.OrderByDescending(c => c.UserScore).ThenByDescending(c => c.CreatedAt);
            }
            else if (selectedIndex == 3)
            {
                sorted = sorted.OrderBy(c => c.UserScore).ThenByDescending(c => c.CreatedAt);
            }

            LvComments.ItemsSource = sorted.ToList();
        }

        private void CmbSortComments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyCommentsSort();
        }

        private async Task LoadPosterAsync(Movie movie)
        {
            if (string.IsNullOrEmpty(movie.PosterUrl)) return;

            try
            {
                var localPath = await PosterCacheService.GetOrDownloadAsync(
                    _http, movie.Id, movie.PosterUrl);

                if (localPath == null) return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(localPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                PosterImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Детали] Ошибка загрузки постера: {ex.Message}");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        private void OnDragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnShare_Click(object sender, RoutedEventArgs e)
        {
            var shareDialog = new ShareMovieDialog(_userId, _movieId);
            shareDialog.Owner = this;
            shareDialog.ShowDialog();
        }

        private void BtnUserCommentProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not CommentDisplayItem item) return;
            if (item.IsPrivate) return;

            var profileWin = new UserProfileWindow(_userId, item.UserId);
            profileWin.Owner = this;
            profileWin.ShowDialog();
        }

        private async void BtnAddComment_Click(object sender, RoutedEventArgs e)
        {
            var commentText = TxtNewComment.Text.Trim();
            if (string.IsNullOrWhiteSpace(commentText))
            {
                MessageBox.Show("Пожалуйста, введите текст комментария.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (commentText.Length > 800)
            {
                MessageBox.Show("Комментарий не может быть длиннее 800 символов.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var db = new ApplicationDbContext();
                var hasRated = await db.Ratings.AnyAsync(r => r.UserId == _userId && r.MovieId == _movieId);
                if (!hasRated)
                {
                    MessageBox.Show("Вы не можете комментировать фильм, пока не оцените его.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingComment = await db.Comments.FirstOrDefaultAsync(c => c.UserId == _userId && c.MovieId == _movieId);
                if (existingComment != null)
                {
                    existingComment.Text = commentText;
                    existingComment.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    var comment = new Comment
                    {
                        UserId = _userId,
                        MovieId = _movieId,
                        Text = commentText,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Comments.Add(comment);
                }

                await db.SaveChangesAsync();
                await LoadCommentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении комментария: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteComment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var existingComment = await db.Comments.FirstOrDefaultAsync(c => c.UserId == _userId && c.MovieId == _movieId);
                if (existingComment != null)
                {
                    db.Comments.Remove(existingComment);
                    await db.SaveChangesAsync();
                }

                TxtNewComment.Text = string.Empty;
                BtnSubmitComment.Content = "Отправить";
                BtnDeleteComment.Visibility = Visibility.Collapsed;

                await LoadCommentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении комментария: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteCommentListItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int commentId) return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить этот комментарий?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _userId);
                bool isCurrentAdminOrMod = currentUser != null && (currentUser.Role == "Admin" || currentUser.Role == "Moderator");

                var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
                if (comment != null)
                {
                    if (comment.UserId == _userId || isCurrentAdminOrMod)
                    {
                        db.Comments.Remove(comment);
                        await db.SaveChangesAsync();

                        if (comment.UserId == _userId)
                        {
                            TxtNewComment.Text = string.Empty;
                            BtnSubmitComment.Content = "Отправить";
                            BtnDeleteComment.Visibility = Visibility.Collapsed;
                        }

                        await LoadCommentsAsync();
                    }
                    else
                    {
                        MessageBox.Show("У вас нет прав для удаления этого комментария.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении комментария: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class CommentDisplayItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserLogin { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int UserScore { get; set; }
        public string RatingText { get; set; } = string.Empty;
        public Visibility RatingVisibility { get; set; } = Visibility.Collapsed;
        public string CreatedAtText => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        public string? AvatarBase64 { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsNotPrivate => !IsPrivate;
        public Visibility DeleteVisibility { get; set; } = Visibility.Collapsed;
    }
}