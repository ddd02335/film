using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public partial class AdminUsersPage : Page
    {
        private User? _selectedUser;
        private readonly string _currentUserRole;
        private readonly int _currentUserId;

        public AdminUsersPage(string currentUserRole, int currentUserId)
        {
            InitializeComponent();
            _currentUserRole = currentUserRole;
            _currentUserId = currentUserId;
            ApplyModeratorRestrictions();
            Loaded += AdminUsersPage_Loaded;
        }

        private void ApplyModeratorRestrictions()
        {
            if (_currentUserRole == "Moderator")
            {
                // модератор может добавлять только обычных пользователей
                var itemsToRemove = CmbAddRole.Items.Cast<ComboBoxItem>()
                    .Where(i => i.Content.ToString() == "Admin" || i.Content.ToString() == "Moderator")
                    .ToList();
                foreach (var item in itemsToRemove) CmbAddRole.Items.Remove(item);
                CmbAddRole.SelectedIndex = 0;

                // модератор может изменять роль только на "user"
                var editItemsToRemove = CmbRoles.Items.Cast<ComboBoxItem>()
                    .Where(i => i.Content.ToString() == "Admin" || i.Content.ToString() == "Moderator")
                    .ToList();
                foreach (var item in editItemsToRemove) CmbRoles.Items.Remove(item);
            }
        }

        private void AdminUsersPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }

        private void LoadUsers()
        {
            try
            {
                using var context = new ApplicationDbContext();
                var users = context.Users.ToList();
                UsersGrid.ItemsSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User user)
            {
                _selectedUser = user;
                TxtSelectedLogin.Text = user.Login;
                TxtEditPassword.Text = user.Password;
                
                // найти и выбрать роль в combobox
                bool roleFound = false;
                foreach (ComboBoxItem item in CmbRoles.Items)
                {
                    if (item.Content.ToString() == user.Role)
                    {
                        CmbRoles.SelectedItem = item;
                        roleFound = true;
                        break;
                    }
                }
                if (!roleFound) CmbRoles.SelectedIndex = -1;

                // логика rbac и защиты состояния
                
                // 1. защита администратора от саморазжалования
                if (_currentUserRole == "Admin" && user.Id == _currentUserId)
                {
                    BtnSave.IsEnabled = true;
                    TxtEditPassword.IsEnabled = true;
                    CmbRoles.IsEnabled = false; // нельзя менять собственную роль
                }
                // 2. ограничения для модератора: нельзя редактировать админов и других модераторов
                else if (_currentUserRole == "Moderator" && (user.Role == "Admin" || user.Role == "Moderator"))
                {
                    BtnSave.IsEnabled = false;
                    TxtEditPassword.IsEnabled = false;
                    CmbRoles.IsEnabled = false;
                }
                // 3. обычный режим редактирования
                else
                {
                    BtnSave.IsEnabled = true;
                    TxtEditPassword.IsEnabled = true;
                    CmbRoles.IsEnabled = true;
                }
            }
            else
            {
                _selectedUser = null;
                TxtSelectedLogin.Text = "Не выбран";
                TxtEditPassword.Text = string.Empty;
                CmbRoles.SelectedIndex = -1;
                
                // если пользователь не выбран отключаем поля редактирования
                BtnSave.IsEnabled = false;
                TxtEditPassword.IsEnabled = false;
                CmbRoles.IsEnabled = false;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null || CmbRoles.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите пользователя и укажите данные для сохранения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtEditPassword.Text))
            {
                MessageBox.Show("Пароль не может быть пустым.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TxtEditPassword.Text.Trim().Length < 6)
            {
                MessageBox.Show("Пароль должен содержать минимум 6 символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new ApplicationDbContext();
                var userToUpdate = context.Users.FirstOrDefault(u => u.Id == _selectedUser.Id);
                
                if (userToUpdate != null)
                {
                    string newRole = ((ComboBoxItem)CmbRoles.SelectedItem).Content.ToString()!;
                    string newPassword = TxtEditPassword.Text.Trim();

                    userToUpdate.Role = newRole;
                    userToUpdate.Password = newPassword;
                    
                    context.SaveChanges();

                    MessageBox.Show("Данные пользователя успешно обновлены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers(); // обновляем список, показать новый пароль
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            string login = TxtAddLogin.Text.Trim();
            string password = TxtAddPassword.Text.Trim();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Логин и пароль не могут быть пустыми.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Пароль должен содержать минимум 6 символов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbAddRole.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите роль для нового пользователя.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new ApplicationDbContext();

                // проверка на уникальность логина
                if (context.Users.Any(u => u.Login == login))
                {
                    MessageBox.Show("Пользователь с таким логином уже существует.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string role = ((ComboBoxItem)CmbAddRole.SelectedItem).Content.ToString()!;

                var newUser = new User
                {
                    Login = login,
                    Password = password,
                    Role = role
                };

                context.Users.Add(newUser);
                context.SaveChanges();

                MessageBox.Show("Пользователь успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                TxtAddLogin.Text = string.Empty;
                TxtAddPassword.Text = string.Empty;
                CmbAddRole.SelectedIndex = 0;

                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void TxtSearchUser_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UsersGrid == null) return;

            string searchText = TxtSearchUser.Text;
            if (string.IsNullOrEmpty(searchText) || searchText == "Поиск по логину...")
            {
                LoadUsers();
                return;
            }

            try
            {
                using var context = new ApplicationDbContext();
                var filteredUsers = context.Users
                    .Where(u => u.Login.ToLower().Contains(searchText.ToLower()))
                    .ToList();
                UsersGrid.ItemsSource = filteredUsers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка фильтрации: {ex.Message}");
            }
        }

        private void TxtSearchUser_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearchUser.Text == "Поиск по логину...")
            {
                TxtSearchUser.Text = string.Empty;
                TxtSearchUser.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void TxtSearchUser_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearchUser.Text))
            {
                TxtSearchUser.Text = "Поиск по логину...";
                TxtSearchUser.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
    }
}