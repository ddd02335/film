// loginwindow.xaml.cs логика окна авторизации
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;

namespace MovieApp
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            TxtLogin.Focus();
        }

        private void TxtBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) AttemptLogin();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e) => AttemptLogin();

        private async void AttemptLogin()
        {
            string login    = TxtLogin.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Введите логин и пароль");
                return;
            }

            try
            {
                LoginProgressRing.Visibility = Visibility.Visible;
                BtnLogin.IsEnabled = false;

                using var db = new ApplicationDbContext();
                var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login && u.Password == password);

                if (user == null)
                {
                    ShowError("Неверный логин или пароль");
                    TxtPassword.Clear();
                    return;
                }

                if (user.IsDeleted)
                {
                    ShowError("Этот аккаунт был архивирован. Обратитесь к администратору.");
                    TxtPassword.Clear();
                    return;
                }

                // авторизация успешна открываем главное окно и закрываем
                var main = new MainWindow(user.Login, user.Role, user.Id);
                main.Show();
                Close();
            }
            catch (System.Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
            finally
            {
                LoginProgressRing.Visibility = Visibility.Collapsed;
                BtnLogin.IsEnabled = true;
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text       = message;
            TxtError.Visibility = Visibility.Visible;
        }

        // ── переход к регистрации ─────────────────────────────────────────────
        private void BtnGoToRegister_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new RegisterWindow();
            registerWindow.Show();
            Close();
        }

        // ── логика "показать пароль" ──────────────────────────────────────────

        private bool _isSyncing = false;

        private void ChkShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            TxtPasswordVisible.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtPasswordVisible.Visibility = Visibility.Visible;
            TxtPasswordVisible.Focus();
        }

        private void ChkShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtPassword.Password = TxtPasswordVisible.Text;
            TxtPasswordVisible.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;
            TxtPassword.Focus();
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncing) return;
            _isSyncing = true;
            TxtPasswordVisible.Text = TxtPassword.Password;
            _isSyncing = false;
        }

        private void TxtPasswordVisible_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isSyncing) return;
            _isSyncing = true;
            TxtPassword.Password = TxtPasswordVisible.Text;
            _isSyncing = false;
        }
    }
}