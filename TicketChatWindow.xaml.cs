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

namespace MovieApp
{
    public partial class TicketChatWindow : Window
    {
        private readonly int _ticketId;
        private readonly int _currentUserId;
        private readonly bool _isAdmin;
        private readonly string _inputPlaceholder = "Введите сообщение...";

        public TicketChatWindow(int ticketId, int currentUserId, bool isAdmin)
        {
            InitializeComponent();
            _ticketId = ticketId;
            _currentUserId = currentUserId;
            _isAdmin = isAdmin;

            Loaded += async (s, e) => {
                ResetInputPlaceholder();
                if (_isAdmin)
                {
                    BtnCloseTicket.Visibility = Visibility.Visible;
                }
                await LoadChatMessagesAsync();
            };
        }

        private async Task LoadChatMessagesAsync()
        {
            try
            {
                ChatProgressRing.Visibility = Visibility.Visible;
                await using var db = new ApplicationDbContext();
                var ticket = await db.SupportTickets
                    .Include(t => t.Messages)
                        .ThenInclude(m => m.Sender)
                    .FirstOrDefaultAsync(t => t.Id == _ticketId);

                if (ticket == null)
                {
                    MessageBox.Show("Обращение не найдено.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                TxtTicketSubject.Text = ticket.Subject;
                TxtTicketStatus.Text = $"Статус: {ticket.Status}";

                if (ticket.Status == "Закрыто")
                {
                    TxtMessageInput.IsEnabled = false;
                    BtnSendMessage.IsEnabled = false;
                    BtnCloseTicket.IsEnabled = false;
                    TxtMessageInput.Text = "Обращение закрыто.";
                }

                var displayItems = ticket.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new ChatMessageDisplayItem
                    {
                        SenderLogin = m.Sender?.Login ?? "Система",
                        Text = m.Text,
                        TimeText = m.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                        Alignment = m.SenderId == _currentUserId ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        BackgroundBrush = m.SenderId == _currentUserId 
                            ? (Brush)new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1D)) // dark red
                            : (Brush)new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3A)), // dark blue/grey
                        BorderBrush = m.SenderId == _currentUserId 
                            ? (Brush)new SolidColorBrush(Color.FromRgb(0xE5, 0x09, 0x14)) // red border
                            : (Brush)new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)), // blue border
                        Margin = m.SenderId == _currentUserId 
                            ? new Thickness(40, 2, 0, 2)
                            : new Thickness(0, 2, 40, 2)
                    })
                    .ToList();

                LvMessages.ItemsSource = displayItems;
                if (displayItems.Count > 0)
                {
                    LvMessages.ScrollIntoView(displayItems.Last());
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при загрузке сообщений. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ChatProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendChatMessageAsync();
        }

        private async void TxtMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SendChatMessageAsync();
            }
        }

        private async Task SendChatMessageAsync()
        {
            var text = TxtMessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text) || text == _inputPlaceholder)
            {
                return;
            }

            try
            {
                BtnSendMessage.IsEnabled = false;
                ChatProgressRing.Visibility = Visibility.Visible;
                await using var db = new ApplicationDbContext();
                
                // Проверяем текущий статус тикета перед отправкой
                var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == _ticketId);
                if (ticket == null || ticket.Status == "Закрыто")
                {
                    MessageBox.Show("Нельзя отправить сообщение в закрытое обращение.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var message = new SupportMessage
                {
                    TicketId = _ticketId,
                    SenderId = _currentUserId,
                    Text = text,
                    CreatedAt = DateTime.UtcNow
                };

                db.SupportMessages.Add(message);
                await db.SaveChangesAsync();

                TxtMessageInput.Text = string.Empty;
                await LoadChatMessagesAsync();
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSendMessage.IsEnabled = true;
                ChatProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnCloseTicket_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Вы действительно хотите закрыть это обращение?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await using var db = new ApplicationDbContext();
                var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == _ticketId);
                if (ticket != null)
                {
                    ticket.Status = "Закрыто";
                    await db.SaveChangesAsync();
                }
                await LoadChatMessagesAsync();
            }
            catch (Exception)
            {
                MessageBox.Show("Произошла ошибка при сохранении данных в базу. Пожалуйста, попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Placeholder Handlers
        private void ResetInputPlaceholder()
        {
            TxtMessageInput.Text = _inputPlaceholder;
            TxtMessageInput.Foreground = Brushes.Gray;
        }

        private void TxtMessageInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtMessageInput.Text == _inputPlaceholder)
            {
                TxtMessageInput.Text = string.Empty;
                TxtMessageInput.Foreground = Brushes.White;
            }
        }

        private void TxtMessageInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtMessageInput.Text))
            {
                ResetInputPlaceholder();
            }
        }
    }

    public class ChatMessageDisplayItem
    {
        public string SenderLogin { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public HorizontalAlignment Alignment { get; set; }
        public Brush BackgroundBrush { get; set; } = Brushes.Transparent;
        public Brush BorderBrush { get; set; } = Brushes.Transparent;
        public Thickness Margin { get; set; }
    }
}
