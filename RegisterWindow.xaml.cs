using System.Windows;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            var login = TxtLogin.Text.Trim();
            var pass = TxtPassword.Password;
            var confirmPass = TxtConfirmPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(pass))
            {
                ShowError("Логин и пароль не могут быть пустыми.");
                return;
            }

            if (pass.Length < 6)
            {
                ShowError("Пароль должен содержать минимум 6 символов.");
                return;
            }

            if (pass != confirmPass)
            {
                ShowError("Пароли не совпадают.");
                return;
            }

            BtnRegister.IsEnabled = false;

            try
            {
                await using var db = new ApplicationDbContext();
                
                // проверка на существующий логин
                bool userExists = await db.Users.AnyAsync(u => u.Login == login);
                if (userExists)
                {
                    ShowError("Пользователь с таким логином уже существует.");
                    BtnRegister.IsEnabled = true;
                    return;
                }

                // создание пользователя
                var newUser = new User
                {
                    Login = login,
                    Password = pass, // в реальном приложении здесь должен быть надёжный хэш (например, bcrypt)
                    Role = "User"
                };

                db.Users.Add(newUser);
                await db.SaveChangesAsync();

                MessageBox.Show(
                    "Регистрация прошла успешно! Теперь вы можете войти.", 
                    "Успех", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);

                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка регистрации: {ex.Message}");
                BtnRegister.IsEnabled = true;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        // ── логика "показать пароль" ──────────────────────────────────────────

        private bool _isSyncingPass = false;
        private bool _isSyncingConfirm = false;

        private void ChkShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            TxtPasswordVisible.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtPasswordVisible.Visibility = Visibility.Visible;

            TxtConfirmPasswordVisible.Text = TxtConfirmPassword.Password;
            TxtConfirmPassword.Visibility = Visibility.Collapsed;
            TxtConfirmPasswordVisible.Visibility = Visibility.Visible;
        }

        private void ChkShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            TxtPassword.Password = TxtPasswordVisible.Text;
            TxtPasswordVisible.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;

            TxtConfirmPassword.Password = TxtConfirmPasswordVisible.Text;
            TxtConfirmPasswordVisible.Visibility = Visibility.Collapsed;
            TxtConfirmPassword.Visibility = Visibility.Visible;
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingPass) return;
            _isSyncingPass = true;
            TxtPasswordVisible.Text = TxtPassword.Password;
            _isSyncingPass = false;
        }

        private void TxtPasswordVisible_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isSyncingPass) return;
            _isSyncingPass = true;
            TxtPassword.Password = TxtPasswordVisible.Text;
            _isSyncingPass = false;
        }

        private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingConfirm) return;
            _isSyncingConfirm = true;
            TxtConfirmPasswordVisible.Text = TxtConfirmPassword.Password;
            _isSyncingConfirm = false;
        }

        private void TxtConfirmPasswordVisible_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isSyncingConfirm) return;
            _isSyncingConfirm = true;
            TxtConfirmPassword.Password = TxtConfirmPasswordVisible.Text;
            _isSyncingConfirm = false;
        }
    }
}