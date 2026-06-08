using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public partial class SupportTicketsPage : Page
    {
        private readonly string _currentRole;
        private readonly int _currentUserId;

        public SupportTicketsPage(string role, int currentUserId)
        {
            InitializeComponent();
            _currentRole = role;
            _currentUserId = currentUserId;

            Loaded += async (s, e) => await LoadTicketsAsync();
        }

        private async Task LoadTicketsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var tickets = await db.SupportTickets
                    .Include(t => t.User)
                    .OrderByDescending(t => t.Status == "Открыто")
                    .ThenByDescending(t => t.Status == "В работе")
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var displayItems = tickets.Select(t => new AdminTicketDisplayItem
                {
                    Id = t.Id,
                    Subject = t.Subject,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    DisplaySender = t.User != null 
                        ? (t.User.IsDeleted ? $"{t.User.Login} (Удален)" : t.User.Login) 
                        : "Неизвестно"
                }).ToList();

                LvTickets.ItemsSource = displayItems;
                TxtNoTickets.Visibility = displayItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteTicket_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int ticketId) return;

            var confirm = MessageBox.Show("Вы уверены, что хотите удалить это обращение и все его сообщения?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await using var db = new ApplicationDbContext();
                
                // Сначала удаляем сообщения (DeleteBehavior.Restrict)
                var messages = await db.SupportMessages.Where(m => m.TicketId == ticketId).ToListAsync();
                db.SupportMessages.RemoveRange(messages);

                var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId);
                if (ticket != null)
                {
                    db.SupportTickets.Remove(ticket);
                }

                await db.SaveChangesAsync();
                await LoadTicketsAsync();
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LvTickets_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvTickets.SelectedItem is not AdminTicketDisplayItem item) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == item.Id);
                if (ticket != null)
                {
                    // Автоматически меняем статус на "В работе" при открытии админом
                    if (ticket.Status == "Открыто")
                    {
                        ticket.Status = "В работе";
                        await db.SaveChangesAsync();
                    }

                    var chatWin = new TicketChatWindow(ticket.Id, _currentUserId, true);
                    chatWin.Owner = Window.GetWindow(this);
                    chatWin.ShowDialog();

                    await LoadTicketsAsync();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class AdminTicketDisplayItem
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string DisplaySender { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedAtText => CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        public Brush StatusBackground => Status switch
        {
            "Открыто" => (Brush)new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)), // blue
            "В работе" => (Brush)new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)), // orange
            "Закрыто" => (Brush)new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45)), // green
            _ => (Brush)new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44))
        };

        public Brush StatusForeground => Brushes.White;
    }
}