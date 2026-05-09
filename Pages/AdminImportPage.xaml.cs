// pages/adminimportpage.xaml.cs импорт фильмов из кинопоиска с выводом лога
using System.Windows;
using System.Windows.Controls;
using MovieApp.Services.Kinopoisk;

namespace MovieApp.Pages
{
    public partial class AdminImportPage : Page
    {
        public AdminImportPage()
        {
            InitializeComponent();
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = TxtApiKey.Text.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                AppendLog("❌ Ошибка: введите API-ключ Кинопоиска.");
                return;
            }

            if (!int.TryParse(TxtPages.Text.Trim(), out int pages) || pages < 1 || pages > 500)
            {
                AppendLog("❌ Ошибка: введите корректное число страниц (1–500).");
                return;
            }

            BtnImport.IsEnabled = false;
            BtnImport.Content   = "⏳  Импортируем...";

            AppendLog($"▶ Начало импорта из Кинопоиска. Страниц: {pages}");
            AppendLog("  → Загружаем коллекцию TOP_POPULAR_ALL...");

            try
            {
                using var service = new KinopoiskService();
                var (fetched, added, skipped) = await service.ImportMoviesAsync(apiKey, pages);

                AppendLog($"✅ Импорт завершён успешно!");
                AppendLog($"   Получено из API: {fetched} шт.");
                AppendLog($"   Добавлено новых: {added} шт.");
                AppendLog($"   Пропущено (дубликаты/ошибки): {skipped} шт.");
                AppendLog("   Данные сохранены в DDb.sqlite.");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                if (ex.InnerException != null)
                    AppendLog($"   Детали: {ex.InnerException.Message}");
            }
            finally
            {
                BtnImport.IsEnabled = true;
                BtnImport.Content   = "▶  Запустить импорт";
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLog.Text = "";
        }

        private void AppendLog(string line)
        {
            TxtLog.Text += line + "\n";
            // прокрутка к последней строке лога
            LogScroll.ScrollToBottom();
        }
    }
}