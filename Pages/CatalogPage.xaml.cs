// pages/catalogpage.xaml.cs
// каталог фильмов: пагинация с нумерацией, локальный кэш постеров, inline-оценка
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using MovieApp.Data;
using MovieApp.Models;

namespace MovieApp.Pages
{
    public partial class CatalogPage : Page
    {
        // ── пагинация ─────────────────────────────────────────────────────────
        private int _currentPage  = 1;
        private const int ItemsPerPage = 20;
        // максимальное количество видимых номеров страниц (напр. 1 2 3 … 7)
        private const int MaxVisiblePageNumbers = 7;

        private List<MovieViewModel> _allMovies      = new();
        private List<MovieViewModel> _filteredMovies = new();

        // observablecollection текущей страницы (привязана к moviespanel)
        private readonly ObservableCollection<MovieViewModel> _pageMovies = new();

        // observablecollection номеров страниц (привязана к pagenumberspanel)
        private readonly ObservableCollection<PageNumberItem> _pageNumbers = new();

        private readonly int _currentUserId;
        private bool _isParentalControlEnabled;

        // жанры, скрываемые при включённом родительском контроле
        private static readonly HashSet<string> MatureGenres = new(StringComparer.OrdinalIgnoreCase)
        {
            "Ужасы", "Триллер", "Криминал", "Эротика"
        };

        // ── http-клиент для кэша постеров (один на всё приложение) ───────────
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public CatalogPage(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;

            // привязываем коллекции один раз не переназначаем в updatepage
            MoviesPanel.ItemsSource      = _pageMovies;
            PageNumbersPanel.ItemsSource = _pageNumbers;

            // создаём папку кэша постеров при первом запуске
            PosterCacheService.EnsureCacheDirectory();

            Loaded += async (_, _) => await LoadMoviesAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadMoviesAsync()
        {
            await using var db = new ApplicationDbContext();

            await LoadFiltersAsync(db);

            // проверяем состояние родительского контроля для текущего пользователя
            var currentUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _currentUserId);
            _isParentalControlEnabled = currentUser?.IsParentalControlEnabled ?? false;

            var ratedMovieIds = await db.Ratings
                .Where(r => r.UserId == _currentUserId)
                .Select(r => r.MovieId)
                .ToListAsync();

            var movies = await db.Movies
                .Include(m => m.MovieGenres)
                .ThenInclude(mg => mg.Genre)
                .AsNoTracking()
                .OrderBy(m => m.Title)
                .ToListAsync();

            // фильтрация родительского контроля: исключаем 18+ и зрелые жанры
            if (_isParentalControlEnabled)
            {
                movies = movies.Where(m =>
                {
                    // исключаем по возрастному рейтингу
                    if (!string.IsNullOrEmpty(m.AgeRating) && m.AgeRating.Contains("18"))
                        return false;

                    // исключаем по зрелым жанрам
                    bool hasMatureGenre = m.MovieGenres
                        .Any(mg => MatureGenres.Contains(mg.Genre.Name));
                    return !hasMatureGenre;
                }).ToList();
            }

            _allMovies = movies.Select(m => new MovieViewModel(m) { IsRated = ratedMovieIds.Contains(m.Id) }).ToList();

            // применяем фильтр (при первом открытии пусто → всё)
            ApplyFilter();

            PanelEmpty.Visibility = _allMovies.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            // загружаем постеры в фоне, не блокируя ui
            _ = Task.Run(() => PreloadPostersAsync(_allMovies));
        }

        private async Task LoadFiltersAsync(ApplicationDbContext db)
        {
            var genres = await db.Genres
                .AsNoTracking()
                .Select(g => g.Name)
                .OrderBy(n => n)
                .ToListAsync();
            
            genres.Insert(0, "Все жанры");
            CmbGenre.ItemsSource = genres;
            CmbGenre.SelectedIndex = 0;

            CmbType.ItemsSource = new List<string> { "Всё видео", "Только фильмы", "Только сериалы" };
            CmbType.SelectedIndex = 0;

            var sorts = new List<string> 
            { 
                "По умолчанию", 
                "Сначала новые", 
                "Сначала старые", 
                "Лучший рейтинг", 
                "Худший рейтинг" 
            };
            CmbSort.ItemsSource = sorts;
            CmbSort.SelectedIndex = 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        // кэш постеров
        // ══════════════════════════════════════════════════════════════════════

        private async Task PreloadPostersAsync(IReadOnlyList<MovieViewModel> movies)
        {
            // не более 4 параллельных http-запросов
            using var semaphore = new SemaphoreSlim(4);

            var tasks = movies
                .Where(m => !string.IsNullOrEmpty(m.PosterUrl))
                .Select(async movie =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var localPath = await PosterCacheService.GetOrDownloadAsync(
                            _http, movie.Id, movie.PosterUrl!);

                        if (localPath != null)
                            await Dispatcher.InvokeAsync(() => movie.SetLocalPath(localPath));
                    }
                    catch { /* постер не загружен — покажем заглушку */ }
                    finally { semaphore.Release(); }
                });

            await Task.WhenAll(tasks);
        }

        // ══════════════════════════════════════════════════════════════════════
        // пагинация
        // ══════════════════════════════════════════════════════════════════════

        /// <summary> вырезает срез из _filteredmovies для текущей страницы, обновляет observablecollection карточек и нумерацию страниц. </summary>
        private void UpdatePage()
        {
            int totalPages = Math.Max(1,
                (int)Math.Ceiling(_filteredMovies.Count / (double)ItemsPerPage));

            _currentPage = Math.Clamp(_currentPage, 1, totalPages);

            // ── обновляем карточки ─────────────────────────────────────────
            var pageSlice = _filteredMovies
                .Skip((_currentPage - 1) * ItemsPerPage)
                .Take(ItemsPerPage)
                .ToList();

            _pageMovies.Clear();
            foreach (var movie in pageSlice)
                _pageMovies.Add(movie);

            // ── счётчик ────────────────────────────────────────────────────
            TxtCount.Text = $"Найдено: {_filteredMovies.Count}  ·  показано: {_pageMovies.Count}";

            // ── кнопки навигации ───────────────────────────────────────────
            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < totalPages;

            // ── нумерованные кнопки страниц ───────────────────────────────
            RebuildPageNumbers(_currentPage, totalPages);

            // сбрасываем прокрутку в верх
            MainScrollViewer.ScrollToTop();
        }

        /// <summary> формирует список видимых номеров страниц с «окном» вокруг текущей. например при странице 5 из 20: [1] [2] [3] [4] [5] [6] [7] при странице 10 из 20: [7] [8] [9] [10] [11] [12] [13] </summary>
        private void RebuildPageNumbers(int current, int total)
        {
            _pageNumbers.Clear();

            if (total <= 1) return; // одна страница номера не нужны

            // вычисляем диапазон видимых номеров
            int half  = MaxVisiblePageNumbers / 2;
            int start = Math.Max(1, current - half);
            int end   = Math.Min(total, start + MaxVisiblePageNumbers - 1);

            // корректируем start если end упёрся в конец
            start = Math.Max(1, end - MaxVisiblePageNumbers + 1);

            for (int i = start; i <= end; i++)
                _pageNumbers.Add(new PageNumberItem(i, i == current));
        }


        // ── поиск ─────────────────────────────────────────────────────────────

        /// <summary> обработчик изменения текста в поле поиска. срабатывает при каждом нажатии клавиши фильтрация мгновенная. управляет видимостью плейсхолдера "поиск фильма...". </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text;

            SearchPlaceholder.Visibility = string.IsNullOrEmpty(query)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyFilter();
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        /// <summary> фильтрует _allmovies в памяти по подстроке названия, жанру и сортирует. сбрасывает пагинацию на первую страницу. не обращается к бд всё работает на уже загруженном списке. </summary>
        private void ApplyFilter()
        {
            if (_allMovies == null) return;

            var query = SearchBox.Text;
            var filtered = _allMovies.AsEnumerable();

            // поиск по тексту
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(m => m.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            // фильтр по жанру
            var selectedGenre = CmbGenre.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedGenre) && selectedGenre != "Все жанры")
            {
                filtered = filtered.Where(m => m.Genres.Contains(selectedGenre));
            }

            // фильтр по типу видео
            var selectedType = CmbType.SelectedItem as string;
            if (selectedType == "Только фильмы")
                filtered = filtered.Where(m => m.Type == "FILM");
            else if (selectedType == "Только сериалы")
                filtered = filtered.Where(m => m.Type != null && m.Type.Contains("SERIES"));

            // сортировка
            var selectedSort = CmbSort.SelectedItem as string;
            if (selectedSort == "Сначала новые")
                filtered = filtered.OrderByDescending(m => m.Year);
            else if (selectedSort == "Сначала старые")
                filtered = filtered.OrderBy(m => m.Year);
            else if (selectedSort == "Лучший рейтинг")
                filtered = filtered.OrderByDescending(m => m.RatingKinopoisk ?? 0);
            else if (selectedSort == "Худший рейтинг")
                filtered = filtered.OrderBy(m => m.RatingKinopoisk ?? double.MaxValue);
            else
                filtered = filtered.OrderBy(m => m.Title);

            _filteredMovies = filtered.ToList();

            _currentPage = 1;
            UpdatePage();
        }



        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage <= 1) return;
            _currentPage--;
            UpdatePage();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(_filteredMovies.Count / (double)ItemsPerPage);
            if (_currentPage >= totalPages) return;
            _currentPage++;
            UpdatePage();
        }

        /// <summary> клик по кнопке с номером страницы в itemscontrol нумерации. tag кнопки содержит номер страницы. </summary>
        private void PageNumberButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int pageNum)
            {
                _currentPage = pageNum;
                UpdatePage();
            }
        }

        // ── клик по карточке ─────────────────────────────────────────────────

        private async void OnMovieCardClicked(object sender, MouseButtonEventArgs e)
        {
            // если клик был по кнопке «оценить» не открываем окно деталей
            if (e.OriginalSource is FrameworkElement src &&
                FindAncestor<Button>(src) != null) return;

            if (sender is not Border border || border.Tag is not int movieId) return;

            e.Handled = true;

            // загружаем фильм вместе с жанрами из бд
            Movie? movie;
            List<string> genres;
            try
            {
                await using var db = new Data.ApplicationDbContext();
                movie = await db.Movies
                    .AsNoTracking()
                    .Include(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                    .FirstOrDefaultAsync(m => m.Id == movieId);

                if (movie == null) return;

                genres = movie.MovieGenres
                    .Select(mg => mg.Genre.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Каталог] Ошибка загрузки деталей: {ex.Message}");
                return;
            }

            var detailsWindow = new MovieDetailsWindow(movie, genres, _currentUserId)
            {
                Owner = Window.GetWindow(this)
            };
            detailsWindow.ShowDialog();
        }


        // ══════════════════════════════════════════════════════════════════════
        // inline-оценка через popup
        // ══════════════════════════════════════════════════════════════════════

        /// <summary> клик по кнопке «оценить»: открывает popup со звёздами. сбрасывает цвет звёзд и скрывает сообщение об успехе перед открытием. проверяет, не оценивал ли уже пользователь этот фильм. </summary>
        private async void BtnRate_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // не передаём клик к onmoviecardclicked

            if (sender is not Button btn || btn.Tag is not int movieId) return;

            // проверяем, оценивал ли пользователь этот фильм
            try
            {
                await using var db = new ApplicationDbContext();
                bool alreadyRated = await db.Ratings.AnyAsync(r => r.UserId == _currentUserId && r.MovieId == movieId);

                if (alreadyRated)
                {
                    // уже оценено → открываем полноценный RateMovieDialog для редактирования оценки и заметки
                    var movie = await db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movieId);
                    if (movie == null) return;

                    var dialog = new RateMovieDialog(movie.Title, _currentUserId, movieId)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    dialog.ShowDialog();
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Каталог] Ошибка проверки оценки: {ex.Message}");
            }

            // новая оценка → используем быстрый inline popup со звёздами
            var popup = FindSiblingPopup(btn);
            if (popup == null) return;

            // сбрасываем состояние перед каждым открытием:
            // скрываем «оценка сохранена!»
            // сбрасываем цвет всех звёзд в серый
            ResetPopupState(popup);

            popup.IsOpen = true;
        }

        /// <summary> клик по звезде (★) внутри popup. ключевое исправление: findancestor&lt;popup&gt; не работает внутри popup, потому что popup живёт в отдельном визуальном дереве. вместо этого поднимаемся по логическому дереву через logicaltreehelper. </summary>
        private async void StarButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not Button btn) return;

            // получаем оценку из commandparameter ("1".."5")
            if (!int.TryParse(btn.CommandParameter?.ToString(), out int score)) return;

            // получаем id фильма из tag (работает т.к. datacontext popup'а исправлен)
            if (btn.Tag is not int movieId)
            {
                System.Diagnostics.Debug.WriteLine("[Звезда] Tag = null — DataContext Popup не установлен");
                return;
            }

            // поднимаемся по логическому дереву найти popup
            // (visualtreehelper не работает в отдельном дереве popup'а)
            var popup = FindPopupInLogicalTree(btn);
            if (popup == null)
            {
                System.Diagnostics.Debug.WriteLine("[Звезда] Popup не найден в логическом дереве");
                return;
            }

            // подсвечиваем выбранные звёзды оранжевым, остальные серым
            HighlightStars(popup, score);

            // сохраняем оценку в бд асинхронно
            await SaveRatingAsync(_currentUserId, movieId, score);

            var movieVm = _allMovies.FirstOrDefault(m => m.Id == movieId);
            if (movieVm != null)
                movieVm.IsRated = true;

            // показываем подтверждение
            var successBlock = FindChild<TextBlock>(popup.Child, "TxtRatingSuccess");
            if (successBlock != null)
                successBlock.Visibility = Visibility.Visible;

            // закрываем через 800 мс
            await Task.Delay(800);
            popup.IsOpen = false;
        }

        /// <summary> поднимается по логическому дереву wpf начиная от переданного элемента. popup регистрирует себя в логическом дереве родителя, поэтому logicaltreehelper.getparent работает там, где visualtreehelper нет. </summary>
        private static Popup? FindPopupInLogicalTree(DependencyObject element)
        {
            var current = LogicalTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is Popup popup) return popup;
                current = LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary> сбрасывает состояние popup перед открытием: все звёзды серые (#444444) сообщение «оценка сохранена!» скрыто </summary>
        private static void ResetPopupState(Popup popup)
        {
            var grey = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

            var starsPanel = FindChild<StackPanel>(popup.Child);
            if (starsPanel != null)
                foreach (var star in starsPanel.Children.OfType<Button>())
                    star.Foreground = grey;

            var successBlock = FindChild<TextBlock>(popup.Child, "TxtRatingSuccess");
            if (successBlock != null)
                successBlock.Visibility = Visibility.Collapsed;
        }

        /// <summary> сохраняет или обновляет оценку в таблице ratings. использует upsert-логику (проверяем существующую запись). </summary>
        private static async Task SaveRatingAsync(int userId, int movieId, int score)
        {
            try
            {
                await using var db = new ApplicationDbContext();

                var existing = await db.Ratings.FirstOrDefaultAsync(
                    r => r.UserId == userId && r.MovieId == movieId);

                if (existing != null)
                    existing.Score = score;
                else
                    db.Ratings.Add(new Rating
                    {
                        UserId  = userId,
                        MovieId = movieId,
                        Score   = score
                    });

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Оценка] Ошибка сохранения: {ex.Message}");
            }
        }

        /// <summary> подсвечивает звёзды до выбранного значения оранжевым, остальные серым. </summary>
        private static void HighlightStars(Popup popup, int selectedScore)
        {
            // ищем stackpanel со звёздами внутри popup
            var starsPanel = FindChild<StackPanel>(popup.Child);
            if (starsPanel == null) return;

            var stars = starsPanel.Children.OfType<Button>().ToList();
            for (int i = 0; i < stars.Count; i++)
            {
                stars[i].Foreground = i < selectedScore
                    ? new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0x00)) // #ffa500 оранжевый
                    : new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)); // #444444 серый
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>поднимается по визуальному дереву и ищет предка нужного типа.</summary>
        private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is T result) return result;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>ищет popup в том же родительском grid что и кнопка.</summary>
        private static Popup? FindSiblingPopup(DependencyObject btn)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(btn);
            if (parent is not Grid grid) return null;

            foreach (UIElement child in grid.Children)
                if (child is Popup popup) return popup;

            return null;
        }

        /// <summary>рекурсивно ищет дочерний элемент нужного типа (и опционально имени).</summary>
        private static T? FindChild<T>(DependencyObject? parent, string? name = null)
            where T : FrameworkElement
        {
            if (parent == null) return null;

            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T typed && (name == null || typed.Name == name))
                    return typed;

                var result = FindChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // модель элемента нумерации страниц
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary> один элемент в нумерации страниц. number номер страницы (отображается на кнопке). isactive текущая страница (кнопка красная). </summary>
    public sealed class PageNumberItem
    {
        public int  Number   { get; }
        public bool IsActive { get; }

        public PageNumberItem(int number, bool isActive)
        {
            Number   = number;
            IsActive = isActive;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // сервис кэширования постеров на диск
    // ══════════════════════════════════════════════════════════════════════════

    public static class PosterCacheService
    {
        public static readonly string CacheDirectory =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PosterCache");

        public static void EnsureCacheDirectory()
        {
            if (!Directory.Exists(CacheDirectory))
                Directory.CreateDirectory(CacheDirectory);
        }

        /// <summary> возвращает локальный путь к постеру: файл есть → путь с диска (мгновенно, без http); файла нет → скачиваем, сохраняем, возвращаем путь. </summary>
        public static async Task<string?> GetOrDownloadAsync(
            HttpClient http, int movieId, string posterUrl)
        {
            var filePath = Path.Combine(CacheDirectory, $"{movieId}.jpg");

            if (File.Exists(filePath)) return filePath;

            try
            {
                var bytes = await http.GetByteArrayAsync(posterUrl);
                await File.WriteAllBytesAsync(filePath, bytes);
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PosterCache] Ошибка Id={movieId}: {ex.Message}");
                return null;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // view model карточки фильма
    // ══════════════════════════════════════════════════════════════════════════

    public sealed class MovieViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int     Id        { get; }
        public string  Title     { get; }
        public string? PosterUrl { get; }
        public int?    Year      { get; }
        public double? RatingKinopoisk { get; }
        public string? Type      { get; }
        public List<string> Genres { get; }

        private bool _isRated;
        public bool IsRated
        {
            get => _isRated;
            set
            {
                if (_isRated != value)
                {
                    _isRated = value;
                    OnPropertyChanged(nameof(IsRated));
                }
            }
        }

        private string? _localPosterPath;
        public string? LocalPosterPath
        {
            get => _localPosterPath;
            private set
            {
                _localPosterPath = value;
                OnPropertyChanged(nameof(LocalPosterPath));
                OnPropertyChanged(nameof(HasNoPoster));
            }
        }

        public bool HasNoPoster => string.IsNullOrEmpty(LocalPosterPath);

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this,
                new System.ComponentModel.PropertyChangedEventArgs(name));

        public MovieViewModel(Movie m)
        {
            Id        = m.Id;
            Title     = m.Title;
            PosterUrl = string.IsNullOrEmpty(m.PosterUrl) ? null : m.PosterUrl;
            Year      = m.Year;
            RatingKinopoisk = m.RatingKinopoisk;
            Type      = m.Type;
            Genres    = m.MovieGenres?.Select(mg => mg.Genre.Name).ToList() ?? new List<string>();
        }

        public void SetLocalPath(string path) => LocalPosterPath = path;
    }
}