// data/applicationdbcontext.cs главный контекст базы данных ef core
using Microsoft.EntityFrameworkCore;
using MovieApp.Models;

namespace MovieApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        // наборы данных (dbset) по одному на каждую таблицу
        public DbSet<User>       Users       { get; set; }
        public DbSet<Movie>      Movies      { get; set; }
        public DbSet<Genre>      Genres      { get; set; }
        public DbSet<MovieGenre> MovieGenres    { get; set; }
        public DbSet<Rating>     Ratings        { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SupportMessage> SupportMessages { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connStr = "postgresql://neondb_owner:npg_wJ0EFeYUC7og@ep-little-breeze-a23rnnvr.eu-central-1.aws.neon.tech/neondb?sslmode=require";
            if (connStr.StartsWith("postgresql://") || connStr.StartsWith("postgres://"))
            {
                var uri = new System.Uri(connStr);
                var userInfo = uri.UserInfo.Split(':');
                var username = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');
                var sslMode = "Require";
                connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true;";
            }
            options.UseNpgsql(connStr);
        }

        /// <summary>Автоматически добавляет недостающие колонки в БД (безопасно при повторном вызове).</summary>
        public static void ApplyPendingMigrations()
        {
            try
            {
                using var db = new ApplicationDbContext();
                db.Database.EnsureCreated();
                var conn = db.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "ALTER TABLE Users ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0";
                cmd.ExecuteNonQuery();
            }
            catch { /* колонка уже существует — игнорируем */ }
        }

        // fluent api: настройка ключей, ограничений и каскадного удаления
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // === таблица moviegenre: составной первичный ключ ===
            modelBuilder.Entity<MovieGenre>(entity =>
            {
                // составной pk вместо одиночного id
                entity.HasKey(mg => new { mg.MovieId, mg.GenreId });

                // при удалении фильма удаляем связи с жанрами
                entity.HasOne(mg => mg.Movie)
                      .WithMany(m => m.MovieGenres)
                      .HasForeignKey(mg => mg.MovieId)
                      .OnDelete(DeleteBehavior.Cascade);

                // при удалении жанра удаляем связи с фильмами
                entity.HasOne(mg => mg.Genre)
                      .WithMany(g => g.MovieGenres)
                      .HasForeignKey(mg => mg.GenreId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // === таблица ratings: составной первичный ключ ===
            modelBuilder.Entity<Rating>(entity =>
            {
                // один пользователь может оценить один фильм только один раз
                entity.HasKey(r => new { r.UserId, r.MovieId });

                // ограничение на значение оценки: от 1 до 5
                entity.ToTable(t => t.HasCheckConstraint("CK_Rating_Score", "\"Score\" >= 1 AND \"Score\" <= 5"));

                entity.HasOne(r => r.User)
                      .WithMany(u => u.Ratings)
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Movie)
                      .WithMany(m => m.Ratings)
                      .HasForeignKey(r => r.MovieId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // === таблица users: логин должен быть уникальным ===
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Login).IsUnique();
                entity.Property(u => u.Role).HasDefaultValue("User");
            });

            // === таблица genres: название жанра должно быть уникальным ===
            // внимание: valuegeneratednever() убран специально кинопоиск возвращает
            // строки жанров (не id), поэтому id генерирует сам sqlite (autoincrement).
            modelBuilder.Entity<Genre>(entity =>
            {
                entity.HasIndex(g => g.Name).IsUnique();
                // id.valuegeneratedonadd() поведение по умолчанию для int pk в ef core,
                // явно не указываем, sqlite сам назначал rowid.
            });

            // === таблица movies: id = kinopoiskid, приходит из api не генерируется базой ===
            modelBuilder.Entity<Movie>(entity =>
            {
                entity.Property(m => m.Id).ValueGeneratedNever();
            });

            // === таблица friendships ===
            modelBuilder.Entity<Friendship>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.HasOne(f => f.User)
                      .WithMany()
                      .HasForeignKey(f => f.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(f => f.Friend)
                      .WithMany()
                      .HasForeignKey(f => f.FriendId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // === таблица notifications ===
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.HasOne(n => n.Recipient)
                      .WithMany()
                      .HasForeignKey(n => n.RecipientId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(n => n.Sender)
                      .WithMany()
                      .HasForeignKey(n => n.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(n => n.Movie)
                      .WithMany()
                      .HasForeignKey(n => n.MovieId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // === таблица comments ===
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(c => c.Movie)
                      .WithMany()
                      .HasForeignKey(c => c.MovieId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // === таблица supporttickets ===
            modelBuilder.Entity<SupportTicket>(entity =>
            {
                entity.HasKey(st => st.Id);
                entity.HasOne(st => st.User)
                      .WithMany()
                      .HasForeignKey(st => st.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // === таблица supportmessages ===
            modelBuilder.Entity<SupportMessage>(entity =>
            {
                entity.HasKey(sm => sm.Id);
                entity.HasOne(sm => sm.Ticket)
                      .WithMany(st => st.Messages)
                      .HasForeignKey(sm => sm.TicketId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sm => sm.Sender)
                      .WithMany()
                      .HasForeignKey(sm => sm.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}