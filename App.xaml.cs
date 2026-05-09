// app.xaml.cs точка входа приложения, инициализация базы данных и запуск loginwindow
using System.Windows;
using MovieApp.Data;
using MovieApp.Models;
using Microsoft.EntityFrameworkCore;

namespace MovieApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                using (var db = new ApplicationDbContext())
                {
                    // гарантируем наличие бд (создаст, если её нет, но не удалит существующую)
                    db.Database.EnsureCreated();

                    // безопасное добавление колонок reason и adminreply (если их нет)
                    try { db.Database.ExecuteSqlRaw("ALTER TABLE SupportTickets ADD COLUMN Reason TEXT DEFAULT 'Другое';"); } catch { }
                    try { db.Database.ExecuteSqlRaw("ALTER TABLE SupportTickets ADD COLUMN AdminReply TEXT;"); } catch { }

                    // если пользователей нет (новая бд) создаём дефолтных
                    if (!db.Users.Any())
                    {
                        db.Users.AddRange(
                            new User { Login = "admin", Password = "admin", Role = "Admin" },
                            new User { Login = "user",  Password = "1234",  Role = "User"  }
                        );
                        db.SaveChanges();
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске БД: {ex.Message}");
            }

            new LoginWindow().Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Критическая ошибка приложения:\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}", "Сбой программы", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // prevent the app from closing immediately
        }
    }
}