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
    public partial class UserSupportPage : Page
    {
        private readonly int _currentUserId;
        private readonly string _subjectPlaceholder = "Введите тему нового обращения...";

        public UserSupportPage(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;

            Loaded += async (s, e) => {
                ResetSubjectPlaceholder();
                await LoadTicketsAsync();
            };
        }

        private async Task LoadTicketsAsync()
        {
            try
            {
                await using var db = new ApplicationDbContext();
                var tickets = await db.SupportTickets
                    .Where(t => t.UserId == _currentUserId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var displayItems = tickets.Select(t => new UserTicketDisplayItem
                {
                    Id = t.Id,
                    Subject = t.Subject,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                }).ToList();

                LvTickets.ItemsSource = displayItems;
                TxtNoTickets.Visibility = displayItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCreateTicket_Click(object sender, RoutedEventArgs e)
        {
            var subject = TxtNewSubject.Text.Trim();
            if (string.IsNullOrEmpty(subject) || subject == _subjectPlaceholder)
            {
                MessageBox.Show("Пожалуйста, введите тему обращения.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var db = new ApplicationDbContext();
                var ticket = new SupportTicket
                {
                    UserId = _currentUserId,
                    Subject = subject,
                    Status = "Открыто",
                    CreatedAt = DateTime.UtcNow
                };

                db.SupportTickets.Add(ticket);
                await db.SaveChangesAsync();

                ResetSubjectPlaceholder();
                await LoadTicketsAsync();
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
                
                // Сначала удаляем сообщения обращения (т.к. DeleteBehavior.Restrict)
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

        private void LvTickets_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LvTickets.SelectedItem is not UserTicketDisplayItem item) return;

            var chatWin = new TicketChatWindow(item.Id, _currentUserId, false);
            chatWin.Owner = Window.GetWindow(this);
            chatWin.ShowDialog();
            
            // Перезагружаем список после закрытия окна чата
            _ = LoadTicketsAsync();
        }

        // Placeholder Handlers
        private void ResetSubjectPlaceholder()
        {
            TxtNewSubject.Text = _subjectPlaceholder;
            TxtNewSubject.Foreground = Brushes.Gray;
        }

        private void TxtNewSubject_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtNewSubject.Text == _subjectPlaceholder)
            {
                TxtNewSubject.Text = string.Empty;
                TxtNewSubject.Foreground = Brushes.White;
            }
        }

        private void TxtNewSubject_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewSubject.Text))
            {
                ResetSubjectPlaceholder();
            }
        }
    }

    public class UserTicketDisplayItem
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
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