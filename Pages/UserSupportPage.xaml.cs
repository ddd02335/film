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
    public partial class UserSupportPage : Page
    {
        private readonly int _currentUserId;

        public UserSupportPage(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            Loaded += async (s, e) => await LoadTicketsAsync();
        }

        private async Task LoadTicketsAsync()
        {
            try
            {
                using var db = new ApplicationDbContext();
                var tickets = await db.SupportTickets
                    .Where(t => t.UserId == _currentUserId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                LstTickets.ItemsSource = tickets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке истории: {ex.Message}");
            }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = TxtSupportMessage.Text.Trim();
            string reason = ((ComboBoxItem)CmbReason.SelectedItem).Content.ToString()!;

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Пожалуйста, введите текст сообщения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new ApplicationDbContext();
                
                var ticket = new SupportTicket
                {
                    UserId = _currentUserId,
                    Reason = reason,
                    Message = message,
                    Status = "Открыт",
                    CreatedAt = DateTime.Now
                };

                context.SupportTickets.Add(ticket);
                await context.SaveChangesAsync();

                MessageBox.Show("Ваше обращение успешно отправлено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                
                TxtSupportMessage.Text = string.Empty;
                await LoadTicketsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}