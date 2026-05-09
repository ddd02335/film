using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;

namespace MovieApp.Pages
{
    public partial class ProfilePage : Page
    {
        private readonly int _currentUserId;

        public ProfilePage(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            Loaded += ProfilePage_Loaded;
        }

        private void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserProfile();
            LoadAvatar();
        }

        private void LoadAvatar()
        {
            try
            {
                string avatarsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
                string avatarFilePath = Path.Combine(avatarsPath, $"{_currentUserId}.jpg");

                if (File.Exists(avatarFilePath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // critical: ignore cache
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // critical: prevent file locking
                    bitmap.UriSource = new Uri(avatarFilePath);
                    bitmap.EndInit();

                    AvatarBrush.ImageSource = bitmap;
                    AvatarFallbackBackground.Visibility = Visibility.Collapsed;
                    TxtAvatarLetter.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AvatarBrush.ImageSource = null;
                    AvatarFallbackBackground.Visibility = Visibility.Visible;
                    TxtAvatarLetter.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке аватара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnChangeAvatar_Click(object sender, RoutedEventArgs e)
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
                    string avatarsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
                    
                    if (!Directory.Exists(avatarsPath))
                    {
                        Directory.CreateDirectory(avatarsPath);
                    }

                    string targetFilePath = Path.Combine(avatarsPath, $"{_currentUserId}.jpg");
                    
                    File.Copy(sourceFilePath, targetFilePath, true);
                    
                    // обновляем ui после сохранения
                    LoadAvatar();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении аватара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadUserProfile()
        {
            try
            {
                using var context = new ApplicationDbContext();

                // получаем пользователя
                var user = context.Users.FirstOrDefault(u => u.Id == _currentUserId);
                if (user != null)
                {
                    TxtLogin.Text = user.Login;
                    TxtAvatarLetter.Text = string.IsNullOrEmpty(user.Login) ? "?" : user.Login.Substring(0, 1).ToUpper();
                }

                // получаем оценки пользователя вместе с фильмами и жанрами
                var ratings = context.Ratings
                    .Include(r => r.Movie)
                    .ThenInclude(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                    .Where(r => r.UserId == _currentUserId)
                    .ToList();

                int count = ratings.Count;

                if (count > 0)
                {
                    TxtTotalRated.Text = count.ToString();
                    
                    double average = ratings.Average(r => r.Score);
                    TxtAverageScore.Text = Math.Round(average, 1).ToString("0.0");

                    var favoriteGenre = ratings
                        .SelectMany(r => r.Movie.MovieGenres.Select(mg => mg.Genre.Name))
                        .GroupBy(name => name)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault();

                    TxtFavoriteGenre.Text = favoriteGenre ?? "Нет данных";
                }
                else
                {
                    TxtTotalRated.Text = "0";
                    TxtAverageScore.Text = "0.0";
                    TxtFavoriteGenre.Text = "Нет данных";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке профиля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}