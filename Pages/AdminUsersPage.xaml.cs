using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
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

        private async void AdminUsersPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
            await LoadArchivedUsersAsync();
            await LoadDashboardStatsAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Загрузка пользователей (активные + архивированные)
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadUsersAsync()
        {
            try
            {
                await using var context = new ApplicationDbContext();
                var users = await context.Users.Where(u => !u.IsDeleted).ToListAsync();
                MaskPasswordsForModerator(users);
                UsersGrid.ItemsSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadArchivedUsersAsync()
        {
            try
            {
                await using var context = new ApplicationDbContext();
                var archived = await context.Users.Where(u => u.IsDeleted).ToListAsync();
                MaskPasswordsForModerator(archived);
                ArchivedUsersGrid.ItemsSource = archived;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки архива: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDashboardStatsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                int totalUsers = await db.Users.CountAsync(u => !u.IsDeleted);
                int totalRatings = await db.Ratings.CountAsync();
                int totalComments = await db.Comments.CountAsync();

                TxtStatsTotalUsers.Text = totalUsers.ToString();
                TxtStatsTotalRatings.Text = totalRatings.ToString();
                TxtStatsTotalComments.Text = totalComments.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stats error: {ex.Message}");
            }
        }

        /// <summary>Маскировка паролей привилегированных пользователей для модераторов (только in-memory, без SaveChanges).</summary>
        private void MaskPasswordsForModerator(System.Collections.Generic.List<User> users)
        {
            if (_currentUserRole != "Moderator") return;

            foreach (var u in users)
            {
                if (u.Role == "Admin" || u.Role == "Moderator")
                    u.Password = "********";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Выбор пользователя + RBAC
        // ══════════════════════════════════════════════════════════════════════

        private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersGrid.SelectedItem is User user)
            {
                _selectedUser = user;
                TxtSelectedLogin.Text = user.Login;
                TxtEditPassword.Text = user.Password;
                ChkParentalControl.IsChecked = user.IsParentalControlEnabled;
                
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
                    BtnDeleteUser.IsEnabled = false; // нельзя удалить себя
                    TxtEditPassword.IsEnabled = true;
                    CmbRoles.IsEnabled = false; // нельзя менять собственную роль
                    // Rule A: нельзя назначать род. контроль самому себе
                    ChkParentalControl.IsEnabled = false;
                }
                // 2. ограничения для модератора: нельзя редактировать админов и других модераторов
                else if (_currentUserRole == "Moderator" && (user.Role == "Admin" || user.Role == "Moderator"))
                {
                    BtnSave.IsEnabled = false;
                    BtnDeleteUser.IsEnabled = false;
                    TxtEditPassword.IsEnabled = false;
                    CmbRoles.IsEnabled = false;
                    ChkParentalControl.IsEnabled = false;
                }
                // 3. обычный режим редактирования
                else
                {
                    BtnSave.IsEnabled = true;
                    BtnDeleteUser.IsEnabled = true;
                    TxtEditPassword.IsEnabled = true;
                    CmbRoles.IsEnabled = true;
                    // Rule A: нельзя назначать род. контроль самому себе
                    // Rule B: род. контроль только для роли "User"
                    bool isSelf = user.Id == _currentUserId;
                    bool isManagementRole = user.Role == "Admin" || user.Role == "Moderator";
                    ChkParentalControl.IsEnabled = !isSelf && !isManagementRole;
                }
            }
            else
            {
                _selectedUser = null;
                TxtSelectedLogin.Text = "Не выбран";
                TxtEditPassword.Text = string.Empty;
                CmbRoles.SelectedIndex = -1;
                ChkParentalControl.IsChecked = false;
                
                // если пользователь не выбран отключаем поля редактирования
                BtnSave.IsEnabled = false;
                BtnDeleteUser.IsEnabled = false;
                TxtEditPassword.IsEnabled = false;
                CmbRoles.IsEnabled = false;
                ChkParentalControl.IsEnabled = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Сохранение / Архивация / Восстановление / Добавление
        // ══════════════════════════════════════════════════════════════════════

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
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
                await using var context = new ApplicationDbContext();
                var userToUpdate = await context.Users.FirstOrDefaultAsync(u => u.Id == _selectedUser.Id);
                
                if (userToUpdate != null)
                {
                    string newRole = ((ComboBoxItem)CmbRoles.SelectedItem).Content.ToString()!;
                    string newPassword = TxtEditPassword.Text.Trim();

                    userToUpdate.Role = newRole;
                    userToUpdate.Password = newPassword;
                    userToUpdate.IsParentalControlEnabled = ChkParentalControl.IsChecked ?? false;
                    
                    await context.SaveChangesAsync();

                    MessageBox.Show("Данные пользователя успешно обновлены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadUsersAsync();
                    await LoadDashboardStatsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Мягкое удаление: архивация пользователя (IsDeleted = true).</summary>
        private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Пожалуйста, выберите пользователя для архивации.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // нельзя удалить самого себя
            if (_selectedUser.Id == _currentUserId)
            {
                MessageBox.Show("Вы не можете архивировать собственную учётную запись.", "Запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Вы уверены, что хотите архивировать пользователя \"{_selectedUser.Login}\"?\n\nАккаунт будет деактивирован, но все данные (оценки, тикеты) сохранятся. Можно восстановить позже.",
                "Подтверждение архивации",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                await using var context = new ApplicationDbContext();
                var userToArchive = await context.Users.FirstOrDefaultAsync(u => u.Id == _selectedUser.Id);

                if (userToArchive != null)
                {
                    userToArchive.IsDeleted = true;
                    await context.SaveChangesAsync();

                    MessageBox.Show("Пользователь архивирован.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    _selectedUser = null;
                    TxtSelectedLogin.Text = "Не выбран";
                    TxtEditPassword.Text = string.Empty;
                    CmbRoles.SelectedIndex = -1;
                    ChkParentalControl.IsChecked = false;

                    await LoadUsersAsync();
                    await LoadArchivedUsersAsync();
                    await LoadDashboardStatsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при архивации пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Восстановление архивированного пользователя (IsDeleted = false).</summary>
        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivedUsersGrid.SelectedItem is not User archivedUser)
            {
                MessageBox.Show("Пожалуйста, выберите пользователя из архива для восстановления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var context = new ApplicationDbContext();
                var userToRestore = await context.Users.FirstOrDefaultAsync(u => u.Id == archivedUser.Id);

                if (userToRestore != null)
                {
                    userToRestore.IsDeleted = false;
                    await context.SaveChangesAsync();

                    MessageBox.Show($"Пользователь \"{archivedUser.Login}\" успешно восстановлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LoadUsersAsync();
                    await LoadArchivedUsersAsync();
                    await LoadDashboardStatsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAddUser_Click(object sender, RoutedEventArgs e)
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
                await using var context = new ApplicationDbContext();

                // проверка на уникальность логина
                bool exists = await context.Users.AnyAsync(u => u.Login == login);
                if (exists)
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
                await context.SaveChangesAsync();

                MessageBox.Show("Пользователь успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                TxtAddLogin.Text = string.Empty;
                TxtAddPassword.Text = string.Empty;
                CmbAddRole.SelectedIndex = 0;

                await LoadUsersAsync();
                await LoadDashboardStatsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Поиск (фильтрует только активных)
        // ══════════════════════════════════════════════════════════════════════

        private async void TxtSearchUser_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UsersGrid == null) return;

            string searchText = TxtSearchUser.Text;
            if (string.IsNullOrEmpty(searchText) || searchText == "Поиск по логину...")
            {
                await LoadUsersAsync();
                return;
            }

            try
            {
                await using var context = new ApplicationDbContext();
                var filteredUsers = await context.Users
                    .Where(u => !u.IsDeleted && u.Login.ToLower().Contains(searchText.ToLower()))
                    .ToListAsync();
                MaskPasswordsForModerator(filteredUsers);
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