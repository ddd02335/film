// mainwindow.xaml.cs главное окно: навигация по разделам приложения
using System.Windows;
using MovieApp.Pages;

namespace MovieApp
{
    public partial class MainWindow : Window
    {
        private readonly string _currentUsername;
        private readonly string _currentRole;
        private readonly int    _currentUserId;

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

        private void InitializeUserInfo()
        {
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

            NavigateToCatalog();
        }

        private void SetPageTitle(string title) => TxtPageTitle.Text = title;


        private void BtnCatalog_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Каталог фильмов");
            NavigateToCatalog();
        }

        private void BtnWatched_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Просмотренные фильмы");
            MainContent.Content = new WatchedPage(_currentUserId);
        }

        private void BtnRecommendations_Click(object sender, RoutedEventArgs e)
        {
            SetPageTitle("Рекомендации для вас");
            MainContent.Content = new RecommendationsPage(_currentUserId);
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
            MainContent.Content = new SupportTicketsPage(_currentRole);
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