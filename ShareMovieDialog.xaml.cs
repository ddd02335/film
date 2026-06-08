using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;
using MovieApp.Pages;

namespace MovieApp
{
    public partial class ShareMovieDialog : Window
    {
        private readonly int _currentUserId;
        private readonly int _movieId;

        public ShareMovieDialog(int currentUserId, int movieId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            _movieId = movieId;
            Loaded += async (_, _) => await LoadFriendsAsync();
        }

        private async Task LoadFriendsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                // Фильтруем IsDeleted = false для друзей
                var friendships = await db.Friendships
                    .Include(f => f.User)
                    .Include(f => f.Friend)
                    .Where(f => (f.UserId == _currentUserId || f.FriendId == _currentUserId) && f.Status == "Accepted")
                    .ToListAsync();

                var friends = new List<FriendDisplayItem>();
                foreach (var f in friendships)
                {
                    var friend = f.UserId == _currentUserId ? f.Friend : f.User;
                    if (friend != null && !friend.IsDeleted)
                    {
                        friends.Add(new FriendDisplayItem
                        {
                            Id = friend.Id,
                            Login = friend.Login
                        });
                    }
                }

                LbFriends.ItemsSource = friends;
                TxtNoFriends.Visibility = friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке друзей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (LbFriends.SelectedItem is not FriendDisplayItem selectedFriend)
            {
                MessageBox.Show("Пожалуйста, выберите друга.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var db = new ApplicationDbContext();
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                var movie = await db.Movies.FirstOrDefaultAsync(m => m.Id == _movieId);

                if (currentUser == null || movie == null)
                {
                    MessageBox.Show("Ошибка: Пользователь или фильм не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Извлекаем оценку и личную заметку текущего пользователя для этого фильма
                var rating = await db.Ratings.FirstOrDefaultAsync(r => r.UserId == _currentUserId && r.MovieId == _movieId);
                
                string ratingPart = "";
                if (rating != null)
                {
                    ratingPart = $" Моя оценка: {rating.Score}/5.";
                    if (!string.IsNullOrWhiteSpace(rating.PersonalNote))
                    {
                        ratingPart += $" Заметка: \"{rating.PersonalNote}\"";
                    }
                }

                string messageText = $"{currentUser.Login} поделился с вами фильмом \"{movie.Title}\".{ratingPart}";

                db.Notifications.Add(new Notification
                {
                    SenderId = _currentUserId,
                    RecipientId = selectedFriend.Id,
                    Type = "MovieShare",
                    MovieId = _movieId,
                    Message = messageText,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });

                await db.SaveChangesAsync();

                MessageBox.Show($"Фильм успешно отправлен пользователю {selectedFriend.Login}!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
