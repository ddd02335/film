using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public partial class ManageContentPage : Page
    {
        public ManageContentPage()
        {
            InitializeComponent();
            Loaded += ManageContentPage_Loaded;
        }

        private void ManageContentPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadGenres();
            LoadMovies();
        }

        private void LoadGenres()
        {
            try
            {
                using var context = new ApplicationDbContext();
                var genres = context.Genres.OrderBy(g => g.Name).ToList();
                LstAddGenres.ItemsSource = genres;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMovies(string searchText = "")
        {
            try
            {
                using var context = new ApplicationDbContext();
                var query = context.Movies.AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    // поиск по названию (без учета регистра)
                    query = query.Where(m => m.Title.ToLower().Contains(searchText.ToLower()));
                    var movies = query.OrderByDescending(m => m.Id).Take(50).ToList();
                    MoviesGrid.ItemsSource = movies;
                }
                else
                {
                    // топ 100 фильмов
                    var movies = query.OrderByDescending(m => m.Id).Take(100).ToList();
                    MoviesGrid.ItemsSource = movies;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фильмов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = TxtSearch.Text.Trim();
            LoadMovies(searchText);
        }

        private void BtnAddMovie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string title = TxtAddTitle.Text.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    MessageBox.Show("Введите название фильма.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int.TryParse(TxtAddYear.Text.Trim(), out int year);
                double.TryParse(TxtAddRating.Text.Trim().Replace(".", ","), out double rating);
                string poster = TxtAddPoster.Text.Trim();
                string type = ((ComboBoxItem)CmbAddType.SelectedItem).Content.ToString()!;
                
                if (LstAddGenres.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Выберите хотя бы один жанр.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using var context = new ApplicationDbContext();
                
                // генерируем уникальный id вручную, так как база не использует автоинкремент для фильмов
                int newId = (context.Movies.Max(m => (int?)m.Id) ?? 0) + 1;

                var newMovie = new Movie
                {
                    Id = newId,
                    Title = title,
                    Year = year > 0 ? year : null,
                    RatingKinopoisk = rating > 0 ? rating : null,
                    PosterUrl = string.IsNullOrWhiteSpace(poster) ? null : poster,
                    Type = type,
                    AgeRating = "12+" // значение по умолчанию для вручную добавленных фильмов
                };

                context.Movies.Add(newMovie);
                
                // связи с жанрами
                foreach (Genre genre in LstAddGenres.SelectedItems)
                {
                    context.MovieGenres.Add(new MovieGenre
                    {
                        MovieId = newId,
                        GenreId = genre.Id
                    });
                }

                context.SaveChanges();

                MessageBox.Show("Фильм успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // очистка формы
                TxtAddTitle.Clear();
                TxtAddYear.Clear();
                TxtAddRating.Clear();
                TxtAddPoster.Clear();
                LstAddGenres.SelectedItems.Clear();

                LoadMovies();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int movieId)
            {
                var result = MessageBox.Show("Вы уверены, что хотите удалить этот фильм?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var context = new ApplicationDbContext();
                        var movie = context.Movies.FirstOrDefault(m => m.Id == movieId);
                        if (movie != null)
                        {
                            context.Movies.Remove(movie);
                            context.SaveChanges();
                            LoadMovies(TxtSearch.Text.Trim());
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag != null)
                {
                    if (int.TryParse(btn.Tag.ToString(), out int movieId))
                    {
                        var editWindow = new EditMovieWindow(movieId);
                        if (editWindow.ShowDialog() == true)
                        {
                            LoadMovies(TxtSearch.Text.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}