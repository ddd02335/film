using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public class RatingDisplayItem
    {
        public int UserId { get; set; }
        public int MovieId { get; set; }
        public string MovieTitle { get; set; } = "";
        public string ScoreDisplay { get; set; } = "";
        public string PersonalNote { get; set; } = "";
    }

    public partial class ProfilePage : Page
    {
        private readonly int _currentUserId;
        private bool _isLoadingParentalControl;
        private bool _isLoadingPrivacy;
        private readonly System.Collections.ObjectModel.ObservableCollection<RatingDisplayItem> _watchedMovies = new();

        public ProfilePage(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            LvWatchedMovies.ItemsSource = _watchedMovies;
            Loaded += ProfilePage_Loaded;
        }

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserProfileAsync();
            await LoadAvatarAsync();
            await LoadParentalControlStateAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Загрузка / Смена аватара
        // ══════════════════════════════════════════════════════════════════════

        public async Task LoadAvatarAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _currentUserId);

                if (user != null && !string.IsNullOrEmpty(user.AvatarBase64))
                {
                    byte[] binaryData = Convert.FromBase64String(user.AvatarBase64);
                    BitmapImage bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(binaryData))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze();

                    AvatarBrush.ImageSource = bitmap;
                    AvatarFallbackBackground.Visibility = Visibility.Collapsed;
                    TxtAvatarLetter.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AvatarBrush.ImageSource = null;
                    AvatarFallbackBackground.Visibility = Visibility.Visible;
                    TxtAvatarLetter.Visibility = Visibility.Visible;
                    if (user != null)
                    {
                        TxtAvatarLetter.Text = string.IsNullOrEmpty(user.Login) ? "?" : user.Login.Substring(0, 1).ToUpper();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке аватара: {ex.Message}");
                AvatarBrush.ImageSource = null;
                AvatarFallbackBackground.Visibility = Visibility.Visible;
                TxtAvatarLetter.Visibility = Visibility.Visible;
            }
        }

        private async void BtnChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите фото профиля",
                    Filter = "Файлы изображений (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string sourceFilePath = openFileDialog.FileName;

                    // Сжимаем и ресайзим картинку в фоновом потоке
                    byte[] compressedBytes = await Task.Run(() => CompressImageToJpeg(sourceFilePath, 150, 150, 75));
                    string base64String = Convert.ToBase64String(compressedBytes);

                    await using var db = new ApplicationDbContext();
                    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                    if (user != null)
                    {
                        user.AvatarBase64 = base64String;
                        await db.SaveChangesAsync();
                    }

                    await LoadAvatarAsync();
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.LoadAvatarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении аватара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static byte[] CompressImageToJpeg(string sourceFilePath, int maxWidth, int maxHeight, int quality)
        {
            var original = new BitmapImage();
            using (var stream = File.OpenRead(sourceFilePath))
            {
                original.BeginInit();
                original.CacheOption = BitmapCacheOption.OnLoad;
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

            var resized = new TransformedBitmap(original, new System.Windows.Media.ScaleTransform(scale, scale));
            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(resized));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Родительский контроль (только для роли "User")
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadParentalControlStateAsync()
        {
            try
            {
                _isLoadingParentalControl = true;
                await using var db = new ApplicationDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (user != null)
                {
                    // скрываем переключатель для Admin и Moderator — род. контроль только для "User"
                    if (user.Role == "Admin" || user.Role == "Moderator")
                    {
                        ChkParentalControl.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ChkParentalControl.IsChecked = user.IsParentalControlEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoadingParentalControl = false;
            }
        }

        private async void ChkParentalControl_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingParentalControl) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (user != null)
                {
                    user.IsParentalControlEnabled = true;
                    await db.SaveChangesAsync();
                    MessageBox.Show("Детский режим включён.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _isLoadingParentalControl = true;
                ChkParentalControl.IsChecked = false;
                _isLoadingParentalControl = false;
            }
        }

        private async void ChkParentalControl_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingParentalControl) return;

            var dialog = new PasswordPromptDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await using var db = new ApplicationDbContext();
                    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);

                    if (user != null && user.Password == dialog.EnteredPassword)
                    {
                        user.IsParentalControlEnabled = false;
                        await db.SaveChangesAsync();
                        MessageBox.Show("Детский режим отключён.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    else
                    {
                        MessageBox.Show("Неверный пароль. Детский режим остаётся включённым.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            _isLoadingParentalControl = true;
            ChkParentalControl.IsChecked = true;
            _isLoadingParentalControl = false;
        }

        private async void ChkPrivateProfile_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingPrivacy) return;
            try
            {
                await using var db = new ApplicationDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (user != null)
                {
                    user.IsPrivate = true;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при включении приватности: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ChkPrivateProfile_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingPrivacy) return;
            try
            {
                await using var db = new ApplicationDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (user != null)
                {
                    user.IsPrivate = false;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выключении приватности: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ══════════════════════════════════════════════════════════════════════
        //  Inline-редактирование заметок (автосохранение при LostFocus)
        // ══════════════════════════════════════════════════════════════════════

        private async void NoteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            if (textBox.DataContext is not RatingDisplayItem item) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == item.UserId && r.MovieId == item.MovieId);
                if (rating != null)
                {
                    string? newNote = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text.Trim();
                    if (rating.PersonalNote != newNote)
                    {
                        rating.PersonalNote = newNote;
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Загрузка профиля и списка просмотренных
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadUserProfileAsync()
        {
            try
            {
                _watchedMovies.Clear();

                await using var context = new ApplicationDbContext();

                var user = await context.Users
                    .Include(u => u.Ratings)
                    .ThenInclude(r => r.Movie)
                    .FirstOrDefaultAsync(u => u.Id == _currentUserId);

                if (user != null)
                {
                    TxtLogin.Text = user.Login;
                    TxtAvatarLetter.Text = string.IsNullOrEmpty(user.Login) ? "?" : user.Login.Substring(0, 1).ToUpper();

                    _isLoadingPrivacy = true;
                    ChkPrivateProfile.IsChecked = user.IsPrivate;
                    _isLoadingPrivacy = false;
                }

                var ratings = await context.Ratings
                    .Include(r => r.Movie)
                    .ThenInclude(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                    .Where(r => r.UserId == _currentUserId)
                    .ToListAsync();

                int count = ratings.Count;

                if (count > 0)
                {
                    TxtTotalRated.Text = count.ToString();
                    
                    double average = ratings.Average(r => r.Score);
                    TxtAverageScore.Text = Math.Round(average, 1).ToString("0.0");

                    var favoriteGenre = ratings
                        .Where(r => r.Movie?.MovieGenres != null)
                        .SelectMany(r => r.Movie.MovieGenres.Select(mg => mg.Genre.Name))
                        .GroupBy(name => name)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault();

                    TxtFavoriteGenre.Text = favoriteGenre ?? "Нет данных";

                    var watchedItems = ratings.Select(r => new RatingDisplayItem
                    {
                        UserId = r.UserId,
                        MovieId = r.MovieId,
                        MovieTitle = r.Movie.Title,
                        ScoreDisplay = $"{r.Score} / 5",
                        PersonalNote = string.IsNullOrWhiteSpace(r.PersonalNote) ? "" : r.PersonalNote
                    }).ToList();

                    foreach (var item in watchedItems)
                    {
                        _watchedMovies.Add(item);
                    }

                    TxtNoRatings.Visibility = Visibility.Collapsed;
                    LvWatchedMovies.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtTotalRated.Text = "0";
                    TxtAverageScore.Text = "0.0";
                    TxtFavoriteGenre.Text = "Нет данных";

                    TxtNoRatings.Visibility = Visibility.Visible;
                    LvWatchedMovies.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке профиля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteRating_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            if (button.DataContext is not RatingDisplayItem item) return;

            if (MessageBox.Show($"Вы уверены, что хотите удалить оценку фильма \"{item.MovieTitle}\" и связанный с ней отзыв?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                await using var db = new ApplicationDbContext();

                var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == _currentUserId && r.MovieId == item.MovieId);
                if (rating != null)
                {
                    db.Ratings.Remove(rating);
                }

                var comment = await db.Comments.FirstOrDefaultAsync(c => c.UserId == _currentUserId && c.MovieId == item.MovieId);
                if (comment != null)
                {
                    db.Comments.Remove(comment);
                }

                await db.SaveChangesAsync();

                await LoadUserProfileAsync();
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при удалении оценки. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}