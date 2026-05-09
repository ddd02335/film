// ratemoviedialog.xaml.cs диалог выбора и сохранения оценки фильма
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp
{
    public partial class RateMovieDialog : Window
    {
        private readonly int _userId;
        private readonly int _movieId;
        private int _selectedScore;

        private readonly List<Button> _stars = new();

        public RateMovieDialog(string movieTitle, int userId, int movieId)
        {
            InitializeComponent();
            _userId  = userId;
            _movieId = movieId;
            TxtMovieName.Text = movieTitle;

            _stars.AddRange(new[] { Star1, Star2, Star3, Star4, Star5 });

            // загружаем текущую оценку если есть
            using var db = new ApplicationDbContext();
            var existing = db.Ratings.FirstOrDefault(r => r.UserId == userId && r.MovieId == movieId);
            if (existing != null)
            {
                SetScore(existing.Score);
            }
        }

        private void Star_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int score))
                SetScore(score);
        }

        private void SetScore(int score)
        {
            _selectedScore   = score;
            BtnSave.IsEnabled = true;
            TxtSelected.Text = score switch
            {
                1 => "⭐  Ужасно",
                2 => "⭐⭐  Плохо",
                3 => "⭐⭐⭐  Нормально",
                4 => "⭐⭐⭐⭐  Хорошо",
                5 => "⭐⭐⭐⭐⭐  Отлично!",
                _ => ""
            };

            // подсвечиваем выбранные звёзды
            for (int i = 0; i < _stars.Count; i++)
            {
                _stars[i].Foreground = new SolidColorBrush(
                    i < score
                        ? Color.FromRgb(0xFF, 0xA5, 0x00)   // #ffa500 оранжевый
                        : Color.FromRgb(0x44, 0x44, 0x44)); // #444444 серый
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            BtnSave.IsEnabled = false;
            BtnSave.Content   = "Сохраняем...";

            using var db = new ApplicationDbContext();
            var existing = db.Ratings.FirstOrDefault(r => r.UserId == _userId && r.MovieId == _movieId);

            if (existing != null)
                existing.Score = _selectedScore;
            else
                db.Ratings.Add(new Rating { UserId = _userId, MovieId = _movieId, Score = _selectedScore });

            await db.SaveChangesAsync();
            Close();
        }
    }
}