using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.App.Services;
using Porta.Core.Media;

namespace Porta.App.ViewModels;

/// <summary>
/// Вкладка «Медиа»: поиск фото и видео по устройству. Срез 1 — только поиск, без превью
/// и EXIF. См. docs/features/25-media-scan.md.
/// </summary>
public partial class MediaViewModel : ViewModelBase
{
    /// <summary>Сколько результатов показываем, чтобы список оставался отзывчивым.</summary>
    public const int ResultLimit = 500;

    /// <summary>
    /// Размер пачки, которой находки уезжают в UI. Раньше список наполнялся одним
    /// куском в конце, и большой обход выглядел как зависание.
    /// См. docs/features/33-progress-and-cancel.md.
    /// </summary>
    public const int BatchSize = 25;

    private readonly MediaScanner _scanner;
    private readonly IUiDispatcher _dispatcher;
    private CancellationTokenSource? _scan;
    private readonly Action<IReadOnlyList<string>>? _onSend;

    public MediaViewModel(
        MediaScanner? scanner = null,
        IUiDispatcher? dispatcher = null,
        string? defaultRoot = null,
        Action<IReadOnlyList<string>>? onSend = null)
    {
        _onSend = onSend;
        _scanner = scanner ?? new MediaScanner();
        _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
        RootPath = defaultRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public ObservableCollection<MediaItem> Results { get; } = [];

    /// <summary>Корень поиска — по умолчанию домашняя папка пользователя.</summary>
    [ObservableProperty]
    public partial string RootPath { get; set; }

    [ObservableProperty]
    public partial bool IncludePhotos { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeVideos { get; set; } = true;

    /// <summary>Подстрока в имени файла.</summary>
    [ObservableProperty]
    public partial string NameFilter { get; set; } = string.Empty;

    /// <summary>Искать только файлы не старше стольких дней (пусто — без ограничения).</summary>
    [ObservableProperty]
    public partial string MaxAgeDays { get; set; } = string.Empty;

    /// <summary>Искать только файлы не меньше стольких мегабайт.</summary>
    [ObservableProperty]
    public partial string MinSizeMb { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
            return;
        if (string.IsNullOrWhiteSpace(RootPath))
        {
            StatusMessage = "Укажите папку для поиска";
            return;
        }

        if (!IncludePhotos && !IncludeVideos)
        {
            StatusMessage = "Выберите хотя бы один тип: фото или видео";
            return;
        }

        MediaQuery query = BuildQuery();
        string root = RootPath.Trim();

        Results.Clear();
        IsScanning = true;
        StatusMessage = "Поиск…";

        using var cts = new CancellationTokenSource();
        _scan = cts;
        try
        {
            // Пачками, а не одним куском в конце: человек видит находки по ходу обхода.
            var batch = new List<MediaFile>(BatchSize);
            int shown = 0;
            bool cancelled = false;

            await Task.Run(() =>
            {
                foreach (MediaFile file in Enumerate(root, query, cts.Token, out cancelled))
                {
                    batch.Add(file);
                    if (batch.Count < BatchSize)
                        continue;

                    Publish(batch, ref shown);
                }
            }, CancellationToken.None);

            Publish(batch, ref shown);
            int total = shown;
            _dispatcher.Post(() => StatusMessage = cancelled
                ? $"Остановлено, найдено: {total}"
                : total >= ResultLimit
                    ? $"Показаны первые {ResultLimit} — уточните фильтры"
                    : $"Найдено: {total}");
        }
        catch (Exception ex)
        {
            _dispatcher.Post(() => StatusMessage = "Ошибка поиска: " + ex.Message);
        }
        finally
        {
            _scan = null;
            IsScanning = false;
        }
    }

    /// <summary>Отдать накопленную пачку в UI-поток и очистить её.</summary>
    private void Publish(List<MediaFile> batch, ref int shown)
    {
        if (batch.Count == 0)
            return;

        MediaItem[] items = batch.Select(MediaItem.From).ToArray();
        batch.Clear();
        shown += items.Length;
        _dispatcher.Post(() =>
        {
            foreach (MediaItem item in items)
                Results.Add(item);
        });
    }

    /// <summary>
    /// Обход с перехватом отмены: найденное до «Стопа» не теряется — человек нажал
    /// кнопку именно потому, что уже увидел нужное.
    /// </summary>
    private IEnumerable<MediaFile> Enumerate(
        string root, MediaQuery query, CancellationToken cancellationToken, out bool cancelled)
    {
        var found = new List<MediaFile>();
        cancelled = false;
        try
        {
            foreach (MediaFile file in _scanner.Scan(root, query, cancellationToken: cancellationToken))
                found.Add(file);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        found.Sort((a, b) => b.ModifiedAt.CompareTo(a.ModifiedAt));
        return found;
    }

    [RelayCommand]
    private void Stop() => _scan?.Cancel();

    /// <summary>
    /// Переложить найденное в список отправки. Не отправляет само: получателя всё равно
    /// выбирать на вкладке «Передача». См. docs/features/34-ui-polish.md.
    /// </summary>
    [RelayCommand]
    private void SendFound()
    {
        if (_onSend is null || Results.Count == 0)
        {
            StatusMessage = "Нечего отправлять";
            return;
        }

        _onSend(Results.Select(r => r.FullPath).ToList());
        StatusMessage = $"Добавлено к отправке: {Results.Count}. Откройте вкладку «Передача»";
    }

    private MediaQuery BuildQuery()
    {
        var kinds = new List<MediaKind>();
        if (IncludePhotos)
            kinds.Add(MediaKind.Photo);
        if (IncludeVideos)
            kinds.Add(MediaKind.Video);

        return new MediaQuery
        {
            Kinds = kinds,
            NameContains = string.IsNullOrWhiteSpace(NameFilter) ? null : NameFilter.Trim(),
            ModifiedFrom = int.TryParse(MaxAgeDays, out int days) && days > 0
                ? DateTimeOffset.UtcNow.AddDays(-days)
                : null,
            MinSize = double.TryParse(MinSizeMb, out double mb) && mb > 0
                ? (long)(mb * 1024 * 1024)
                : 0,
            MaxResults = ResultLimit,
        };
    }
}

/// <summary>Строка списка найденного медиа.</summary>
public sealed record MediaItem(
    string FileName,
    string FullPath,
    string DisplayPath,
    string Kind,
    string Size,
    string ModifiedAt)
{
    /// <summary>
    /// Длинный путь режем СЛЕВА: хвост (папка и имя файла) информативнее начала,
    /// а TextTrimming в разметке умеет только справа. См. docs/features/34-ui-polish.md.
    /// </summary>
    private const int PathDisplayLimit = 70;

    public static MediaItem From(MediaFile file) => new(
        file.FileName,
        file.FullPath,
        Shorten(file.FullPath),
        file.Kind == MediaKind.Photo ? "Фото" : "Видео",
        FormatSize(file.Size),
        file.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));

    private static string Shorten(string path)
        => path.Length <= PathDisplayLimit ? path : "…" + path[^(PathDisplayLimit - 1)..];

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Б",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} КБ",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} МБ",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} ГБ",
    };
}
