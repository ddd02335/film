using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public class FriendDisplayItem
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Initials => string.IsNullOrEmpty(Login) ? "?" : Login.Substring(0, 1).ToUpper();
        public string? AvatarBase64 { get; set; }
        public bool HasAvatar => !string.IsNullOrEmpty(AvatarBase64);
        public bool HasNoAvatar => string.IsNullOrEmpty(AvatarBase64);
        public DateTime LastActivity { get; set; }
        public bool IsOnline => (DateTime.UtcNow - LastActivity.ToUniversalTime()).TotalMinutes <= 5;
        public string StatusText => IsOnline ? "В сети" : "Не в сети";
        public System.Windows.Media.SolidColorBrush StatusColor => IsOnline 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)) // #4CAF50
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)); // #888888
    }

    public class NotificationDisplayItem
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderLogin { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? MovieId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string HeaderText => Type == "MovieShare" ? "Рекомендация фильма" : "Обмен оценками";
        public string TimeText => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        public Visibility IsShareType => Type == "MovieShare" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsImportType => Type == "RatingsShare" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowMarkRead => !IsRead ? Visibility.Visible : Visibility.Collapsed;
    }

    public partial class FriendsPage : Page
    {
        private readonly int _currentUserId;
        private string _placeholderText = "Введите логин пользователя...";

        public FriendsPage(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            ResetSearchPlaceholder();
            Loaded += FriendsPage_Loaded;
        }

        private async void FriendsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAllSocialDataAsync();
        }

        private async Task LoadAllSocialDataAsync()
        {
            await LoadFriendsAsync();
            await LoadRequestsAsync();
            await LoadNotificationsAsync();
        }

        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                await LoadAllSocialDataAsync();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Плейсхолдер для строки поиска
        // ══════════════════════════════════════════════════════════════════════

        private void ResetSearchPlaceholder()
        {
            TxtSearchUser.Text = _placeholderText;
            TxtSearchUser.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void TxtSearchUser_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearchUser.Text == _placeholderText)
            {
                TxtSearchUser.Text = string.Empty;
                TxtSearchUser.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void TxtSearchUser_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearchUser.Text))
            {
                ResetSearchPlaceholder();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Список друзей
        // ══════════════════════════════════════════════════════════════════════

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
                            Login = friend.Login,
                            AvatarBase64 = friend.AvatarBase64,
                            LastActivity = friend.LastActivity
                        });
                    }
                }

                LvFriends.ItemsSource = friends;
                TxtNoFriends.Visibility = friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка друзей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                // Входящие запросы (отправленные мне другими пользователями)
                var incoming = await db.Friendships
                    .Include(f => f.User)
                    .Where(f => f.FriendId == _currentUserId && f.Status == "Pending" && !f.User.IsDeleted)
                    .ToListAsync();

                LvIncomingRequests.ItemsSource = incoming;
                TxtNoIncomingRequests.Visibility = incoming.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                // Исходящие запросы (отправленные мной другим пользователям)
                var outgoing = await db.Friendships
                    .Include(f => f.Friend)
                    .Where(f => f.UserId == _currentUserId && f.Status == "Pending" && !f.Friend.IsDeleted)
                    .ToListAsync();

                LvOutgoingRequests.ItemsSource = outgoing;
                TxtNoOutgoingRequests.Visibility = outgoing.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки запросов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAddFriend_Click(object sender, RoutedEventArgs e)
        {
            var searchLogin = TxtSearchUser.Text.Trim();
            if (string.IsNullOrEmpty(searchLogin) || searchLogin == _placeholderText)
            {
                MessageBox.Show("Введите логин для поиска.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var db = new ApplicationDbContext();
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (currentUser == null) return;

                if (string.Equals(currentUser.Login, searchLogin, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Нельзя отправить запрос самому себе.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Login == searchLogin && !u.IsDeleted && (!u.IsPrivate || currentUser.Role == "Admin" || currentUser.Role == "Moderator"));
                if (targetUser == null)
                {
                    MessageBox.Show("Пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка существующей связи
                var existing = await db.Friendships.FirstOrDefaultAsync(f => 
                    (f.UserId == _currentUserId && f.FriendId == targetUser.Id) ||
                    (f.UserId == targetUser.Id && f.FriendId == _currentUserId));

                if (existing != null)
                {
                    if (existing.Status == "Accepted")
                    {
                        MessageBox.Show("Этот пользователь уже у вас в друзьях.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (existing.UserId == _currentUserId)
                    {
                        MessageBox.Show("Вы уже отправили запрос этому пользователю.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Этот пользователь уже отправил вам запрос. Примите его в списке входящих.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                db.Friendships.Add(new Friendship
                {
                    UserId = _currentUserId,
                    FriendId = targetUser.Id,
                    Status = "Pending"
                });
                await db.SaveChangesAsync();

                MessageBox.Show("Запрос на добавление в друзья успешно отправлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtSearchUser.Text = string.Empty;
                ResetSearchPlaceholder();
                await LoadRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении друга: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAcceptRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int requestId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var req = await db.Friendships.FirstOrDefaultAsync(f => f.Id == requestId);
                if (req != null)
                {
                    req.Status = "Accepted";
                    await db.SaveChangesAsync();
                    MessageBox.Show("Запрос принят. Теперь вы друзья!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadAllSocialDataAsync();
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.CheckNotificationsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeclineRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int requestId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var req = await db.Friendships.FirstOrDefaultAsync(f => f.Id == requestId);
                if (req != null)
                {
                    db.Friendships.Remove(req);
                    await db.SaveChangesAsync();
                    await LoadRequestsAsync();
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.CheckNotificationsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCancelRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int requestId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var req = await db.Friendships.FirstOrDefaultAsync(f => f.Id == requestId);
                if (req != null)
                {
                    db.Friendships.Remove(req);
                    await db.SaveChangesAsync();
                    await LoadRequestsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnShareRatings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int friendId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (currentUser == null) return;

                db.Notifications.Add(new Notification
                {
                    SenderId = _currentUserId,
                    RecipientId = friendId,
                    Type = "RatingsShare",
                    Message = $"{currentUser.Login} поделился(ась) с вами своими оценками.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
                await db.SaveChangesAsync();

                MessageBox.Show("Вы успешно поделились своими оценками с другом!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки оценок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnViewFriendProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int friendId) return;

            var profileWin = new UserProfileWindow(_currentUserId, friendId);
            profileWin.Owner = Window.GetWindow(this);
            profileWin.ShowDialog();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Уведомления
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadNotificationsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                // Фильтруем отправителей, которые не удалены (IsDeleted = false)
                var notifications = await db.Notifications
                    .Include(n => n.Sender)
                    .Where(n => n.RecipientId == _currentUserId && !n.Sender.IsDeleted)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                var listItems = notifications.Select(n => new NotificationDisplayItem
                {
                    Id = n.Id,
                    SenderId = n.SenderId,
                    SenderLogin = n.Sender?.Login ?? "Система",
                    Type = n.Type,
                    MovieId = n.MovieId,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                }).ToList();

                LvNotifications.ItemsSource = listItems;
                TxtNoNotifications.Visibility = listItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                int unreadCount = listItems.Count(item => !item.IsRead);
                if (unreadCount > 0)
                {
                    TxtTabNotificationsHeader.Text = $"Уведомления ({unreadCount})";
                }
                else
                {
                    TxtTabNotificationsHeader.Text = "Уведомления";
                }

                // Обновить также индикатор в MainWindow
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.UpdateNotificationDot(unreadCount > 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки уведомлений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnMarkAsRead_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int notifId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notifId);
                if (notif != null)
                {
                    notif.IsRead = true;
                    await db.SaveChangesAsync();
                    await LoadNotificationsAsync();
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.CheckNotificationsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnViewSharedMovie_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int notifId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notifId);
                if (notif != null)
                {
                    notif.IsRead = true;
                    await db.SaveChangesAsync();

                    if (notif.MovieId.HasValue)
                    {
                        var detailsWin = new MovieDetailsWindow(notif.MovieId.Value, _currentUserId);
                        detailsWin.Owner = Window.GetWindow(this);
                        detailsWin.ShowDialog();
                    }

                    await LoadNotificationsAsync();
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.CheckNotificationsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAcceptRatingsShare_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int notifId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notifId);
                if (notif == null) return;

                int senderId = notif.SenderId;
                var donorRatings = await db.Ratings.Where(r => r.UserId == senderId).ToListAsync();

                var myRatingMovieIds = (await db.Ratings
                    .Where(r => r.UserId == _currentUserId)
                    .Select(r => r.MovieId)
                    .ToListAsync())
                    .ToHashSet();

                int imported = 0;
                foreach (var rating in donorRatings)
                {
                    if (myRatingMovieIds.Contains(rating.MovieId))
                        continue;

                    db.Ratings.Add(new Rating
                    {
                        UserId = _currentUserId,
                        MovieId = rating.MovieId,
                        Score = rating.Score,
                        PersonalNote = rating.PersonalNote
                    });
                    imported++;
                }

                if (imported > 0)
                {
                    await db.SaveChangesAsync();
                }

                notif.IsRead = true;
                await db.SaveChangesAsync();

                MessageBox.Show($"Оценки успешно скопированы! Импортировано новых оценок: {imported}.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadNotificationsAsync();
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    await mainWindow.CheckNotificationsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка копирования оценок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRemoveFriend_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int friendId) return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить этого пользователя из друзей?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var friendship = await db.Friendships.FirstOrDefaultAsync(f => 
                    ((f.UserId == _currentUserId && f.FriendId == friendId) || 
                     (f.UserId == friendId && f.FriendId == _currentUserId)) && 
                    f.Status == "Accepted");

                if (friendship != null)
                {
                    db.Friendships.Remove(friendship);
                    await db.SaveChangesAsync();
                    await LoadFriendsAsync();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteNotification_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int notifId) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notifId);
                if (notif != null)
                {
                    db.Notifications.Remove(notif);
                    await db.SaveChangesAsync();
                    await LoadNotificationsAsync();
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.CheckNotificationsAsync();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
