// открывается при клике по карточке в каталоге
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MovieApp.Models;
using MovieApp.Pages;   // для доступа к postercacheservice

namespace MovieApp
{
    public partial class MovieDetailsWindow : Window
    {
        // переиспользуемый http-клиент (статический один на всё приложение)
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private readonly int _userId;
        private readonly int _movieId;

        public MovieDetailsWindow(Movie movie, IEnumerable<string> genres, int userId)
        {
            InitializeComponent();
            _userId  = userId;
            _movieId = movie.Id;

            // заполняем ui данными фильма
            TxtTitle.Text       = movie.Title;
            TxtYear.Text        = movie.Year.HasValue ? $"{movie.Year} г." : "Год не указан";
            TxtDescription.Text = string.IsNullOrWhiteSpace(movie.Description)
                ? "Описание отсутствует."
                : movie.Description;

            TxtKpRating.Text = movie.RatingKinopoisk.HasValue
                ? movie.RatingKinopoisk.Value.ToString("0.0")
                : "—";

            // жанры через запятую
            var genreList = genres.ToList();
            TxtGenres.Text = genreList.Count > 0
                ? string.Join("  ·  ", genreList)
                : "Жанры не указаны";

            // возрастной рейтинг
            TxtAgeRating.Text = movie.AgeRating ?? "12+";

            // загружаем постер асинхронно после построения окна
            Loaded += async (_, _) => await LoadPosterAsync(movie);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════════

        /// <summary> загружает постер: 1. если файл уже есть в кэше → читаем с диска (мгновенно). 2. если нет → скачиваем через http, сохраняем в кэш. 3. устанавливаем bitmapimage в posterimage.source. </summary>
        private async Task LoadPosterAsync(Movie movie)
        {
            if (string.IsNullOrEmpty(movie.PosterUrl)) return;

            try
            {
                // используем тот же кэш что и каталог (postercacheservice)
                var localPath = await PosterCacheService.GetOrDownloadAsync(
                    _http, movie.Id, movie.PosterUrl);

                if (localPath == null) return;

                // bitmapimage должен создаваться в ui-потоке
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                // cacheonload=ondemand файл читается без блокировки
                bitmap.CacheOption  = BitmapCacheOption.OnLoad;
                bitmap.UriSource    = new Uri(localPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze(); // делаем потокобезопасным

                PosterImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                // постер не загрузился покажется заглушка 🎬
                System.Diagnostics.Debug.WriteLine(
                    $"[Детали] Ошибка загрузки постера: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        /// <summary> перетаскивание безрамочного окна за любую область. срабатывает на mousedown по внешнему border'у. </summary>
        private void OnDragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}