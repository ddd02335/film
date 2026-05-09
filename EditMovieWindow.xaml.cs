using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp
{
    public partial class EditMovieWindow : Window
    {
        private int _movieId;

        public EditMovieWindow(int movieId)
        {
            InitializeComponent();
            _movieId = movieId;
            Loaded += EditMovieWindow_Loaded;
        }

        private void EditMovieWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new ApplicationDbContext();
                
                var allGenres = context.Genres.OrderBy(g => g.Name).ToList();
                LstEditGenres.ItemsSource = allGenres;

                var movie = context.Movies
                    .Include(m => m.MovieGenres)
                    .FirstOrDefault(m => m.Id == _movieId);

                if (movie != null)
                {
                    TxtEditTitle.Text = movie.Title;
                    TxtEditYear.Text = movie.Year?.ToString() ?? "";
                    TxtEditRating.Text = movie.RatingKinopoisk?.ToString() ?? "";
                    TxtEditPoster.Text = movie.PosterUrl ?? "";
                    TxtEditAgeRating.Text = movie.AgeRating ?? "";

                    var movieGenreIds = movie.MovieGenres.Select(mg => mg.GenreId).ToList();
                    foreach (var genre in allGenres)
                    {
                        if (movieGenreIds.Contains(genre.Id))
                        {
                            LstEditGenres.SelectedItems.Add(genre);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке фильма: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new ApplicationDbContext();
                var movie = context.Movies
                    .Include(m => m.MovieGenres)
                    .FirstOrDefault(m => m.Id == _movieId);

                if (movie != null)
                {
                    movie.Title = TxtEditTitle.Text.Trim();
                    
                    if (int.TryParse(TxtEditYear.Text.Trim(), out int year))
                        movie.Year = year;
                    else
                        movie.Year = null;

                    if (double.TryParse(TxtEditRating.Text.Trim().Replace(".", ","), out double rating))
                        movie.RatingKinopoisk = rating;
                    else
                        movie.RatingKinopoisk = null;

                    movie.PosterUrl = TxtEditPoster.Text.Trim();
                    movie.AgeRating = TxtEditAgeRating.Text.Trim();

                    // очистка старых жанров
                    context.MovieGenres.RemoveRange(movie.MovieGenres);

                    // добавление новых
                    foreach (Genre genre in LstEditGenres.SelectedItems)
                    {
                        context.MovieGenres.Add(new MovieGenre
                        {
                            MovieId = movie.Id,
                            GenreId = genre.Id
                        });
                    }

                    context.SaveChanges();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}