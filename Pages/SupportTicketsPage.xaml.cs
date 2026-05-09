using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public partial class SupportTicketsPage : Page
    {
        private readonly string _currentRole;

        public SupportTicketsPage(string role)
        {
            InitializeComponent();
            _currentRole = role;
            
            // скрываем колонку действий, если не админ (или можно оставить, но кнопка внутри будет скрыта)
            // здесь скроем всю колонку для простоты, если роль не admin.
            Loaded += (s, e) => {
                if (_currentRole != "Admin")
                {
                    var col = TicketsGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Действие");
                    if (col != null) col.Visibility = Visibility.Collapsed;
                }
            };

            Loaded += async (s, e) => await LoadTicketsAsync();
        }

        private async Task LoadTicketsAsync()
        {
            try
            {
                using var db = new ApplicationDbContext();
                var tickets = await db.SupportTickets
                    .OrderByDescending(t => t.Status == "Открыт") // сначала открытые
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync();

                TicketsGrid.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки жалоб: {ex.Message}");
            }
        }

        private void TicketsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TicketsGrid.SelectedItem is SupportTicket ticket)
            {
                TxtSelectedMessage.Text = ticket.Message;
                TxtAdminReply.Text = ticket.AdminReply ?? string.Empty;
                
                // включаем панель ответа через IsHitTestVisible/IsReadOnly чтобы избежать "отбеливания" WPF
                TxtAdminReply.IsReadOnly = false;
                BtnSubmitReply.IsHitTestVisible = true;
                BtnSubmitReply.Foreground = System.Windows.Media.Brushes.White;

                if (ticket.Status == "Закрыт")
                {
                    BtnSubmitReply.Content = "Обновить ответ";
                    BtnSubmitReply.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#0078D7"); // blue
                }
                else
                {
                    BtnSubmitReply.Content = "Ответить и закрыть";
                    BtnSubmitReply.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#28A745"); // green
                }
            }
            else
            {
                TxtSelectedMessage.Text = "Выберите обращение из списка";
                TxtAdminReply.Text = string.Empty;
                
                // отключаем панель ответа (имитируем IsEnabled=false)
                TxtAdminReply.IsReadOnly = true;
                BtnSubmitReply.IsHitTestVisible = false;

                // явная установка цветов для неактивного состояния без системных оверлеев
                BtnSubmitReply.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#555555");
                BtnSubmitReply.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#AAAAAA");
                BtnSubmitReply.Content = "Ответить и закрыть";
            }
        }

        private async void BtnSubmitReply_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsGrid.SelectedItem is not SupportTicket selected)
            {
                MessageBox.Show("Пожалуйста, выберите обращение для ответа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtAdminReply.Text))
            {
                MessageBox.Show("Пожалуйста, введите текст ответа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = new ApplicationDbContext();
                var ticket = await db.SupportTickets.FindAsync(selected.Id);
                if (ticket != null)
                {
                    ticket.AdminReply = TxtAdminReply.Text.Trim();
                    ticket.Status = "Закрыт";
                    
                    await db.SaveChangesAsync();
                    MessageBox.Show("Ответ сохранен, обращение закрыто.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    TxtAdminReply.Text = string.Empty;
                    await LoadTicketsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении ответа: {ex.Message}");
            }
        }

        private async void BtnDeleteTicket_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRole != "Admin") return;

            if (sender is Button btn && btn.Tag is int ticketId)
            {
                if (MessageBox.Show("Вы уверены, что хотите безвозвратно удалить это обращение?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using var db = new ApplicationDbContext();
                        var ticket = await db.SupportTickets.FindAsync(ticketId);
                        if (ticket != null)
                        {
                            db.SupportTickets.Remove(ticket);
                            await db.SaveChangesAsync();
                            await LoadTicketsAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}");
                    }
                }
            }
        }
    }
}