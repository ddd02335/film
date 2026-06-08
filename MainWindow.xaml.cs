using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MovieApp.Pages;
using MovieApp.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieApp
{
    public partial class MainWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private readonly string _currentUsername;
        private readonly string _currentRole;
        private readonly int    _currentUserId;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private string? _currentUserAvatarBase64;
        public string? CurrentUserAvatarBase64
        {
            get => _currentUserAvatarBase64;
            set
            {
                _currentUserAvatarBase64 = value;
                OnPropertyChanged(nameof(CurrentUserAvatarBase64));
                OnPropertyChanged(nameof(HasAvatar));
                OnPropertyChanged(nameof(HasNoAvatar));
            }
        }

        public bool HasAvatar => !string.IsNullOrEmpty(CurrentUserAvatarBase64);
        public bool HasNoAvatar => string.IsNullOrEmpty(CurrentUserAvatarBase64);
        public string AvatarLetter => string.IsNullOrEmpty(_currentUsername) ? "?" : _currentUsername.Substring(0, 1).ToUpper();

        private DateTime _lastActivityUpdate = DateTime.MinValue;

        private void UpdateUserActivityThrottled()
        {
            if (_currentUserId <= 0) return;
            var now = DateTime.UtcNow;
            if ((now - _lastActivityUpdate).TotalMinutes >= 1.0)
            {
                _lastActivityUpdate = now;
                Task.Run(async () =>
                {
                    try
                    {
                        await using var db = new ApplicationDbContext();
                        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserId);
                        if (user != null)
                        {
                            user.LastActivity = now;
                            await db.SaveChangesAsync();
                        }
                    }
                    catch { }
                });
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            _currentUsername = "Пользователь";
            _currentRole     = "User";
            _currentUserId   = 0;
            InitializeUserInfo();
        }

        public MainWindow(string username, string role, int userId)
        {
            InitializeComponent();
            _currentUsername = username;
            _currentRole     = role;
            _currentUserId   = userId;
            InitializeUserInfo();
        }

        private async void InitializeUserInfo()
        {
            PreviewMouseDown += (s, e) => UpdateUserActivityThrottled();
            PreviewKeyDown += (s, e) => UpdateUserActivityThrottled();
            UpdateUserActivityThrottled();

            TxtUsername.Text = _currentUsername;
            TxtUserRole.Text = _currentRole;

            if (_currentRole == "Admin")
                BtnImportApi.Visibility = Visibility.Visible;
                
            if (_currentRole != "Admin" && _currentRole != "Moderator")
                BtnNavUsers.Visibility = Visibility.Collapsed;

            if (_currentRole != "Admin" && _currentRole != "Moderator")
                BtnNavContent.Visibility = Visibility.Collapsed;

            if (_currentRole != "Admin" && _currentRole != "Moderator")
                BtnNavTickets.Visibility = Visibility.Collapsed;

            // кнопка «поддержка» скрыта для админа и модератора (они сами поддержка)
            if (_currentRole == "Admin" || _currentRole == "Moderator")
                BtnNavSupport.Visibility = Visibility.Collapsed;

            await LoadAvatarAsync();
            await CheckNotificationsAsync();
            NavigateToCatalog();
        }

        public async Task CheckNotificationsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                bool hasUnread = await db.Notifications
                    .Include(n => n.Sender)
                    .AnyAsync(n => n.RecipientId == _currentUserId && !n.IsRead && !n.Sender.IsDeleted);
                UpdateNotificationDot(hasUnread);
            }
            catch { }
        }

        public async Task LoadAvatarAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _currentUserId);
                if (user != null)
                {
                    CurrentUserAvatarBase64 = user.AvatarBase64;
                }
            }
            catch
            {
                CurrentUserAvatarBase64 = null;
            }
        }

        private void SetPageTitle(string title) => TxtPageTitle.Text = title;


        private void BtnCatalog_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Каталог фильмов");
            NavigateToCatalog();
        }

        private void BtnRecommendations_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Рекомендации для вас");
            MainContent.Content = new RecommendationsPage(_currentUserId);
        }

        private void BtnFriends_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Друзья и Уведомления");
            MainContent.Content = new FriendsPage(_currentUserId);
        }

        public void UpdateNotificationDot(bool show)
        {
            NotificationDot.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnImportApi_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Импорт фильмов из Кинопоиска API");
            MainContent.Content = new AdminImportPage();
        }

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Профиль пользователя");
            MainContent.Content = new ProfilePage(_currentUserId);
        }

        private void BtnNavContent_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Управление контентом");
            MainContent.Content = new ManageContentPage();
        }

        private void BtnNavUsers_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Управление пользователями");
            MainContent.Content = new AdminUsersPage(_currentRole, _currentUserId);
        }

        private void BtnNavSupport_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Служба поддержки");
            MainContent.Content = new UserSupportPage(_currentUserId);
        }

        private void BtnNavTickets_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Жалобы пользователей");
            MainContent.Content = new SupportTicketsPage(_currentRole, _currentUserId);
        }

        private void NavigateToCatalog()
        {
            SetPageTitle("Каталог фильмов");
            MainContent.Content = new CatalogPage(_currentUserId);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}