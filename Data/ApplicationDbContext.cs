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

        // подключение к локальному файлу sqlite
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // файл ddb.sqlite создаётся рядом с .exe при первом запуске
            options.UseSqlite("Data Source=DDb.sqlite");
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
                entity.ToTable(t => t.HasCheckConstraint("CK_Rating_Score", "Score >= 1 AND Score <= 5"));

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
        }
    }
}