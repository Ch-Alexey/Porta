using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private readonly MediaScanner _scanner;
    private readonly IUiDispatcher _dispatcher;
    private CancellationTokenSource? _scan;

    public MediaViewModel(MediaScanner? scanner = null, IUiDispatcher? dispatcher = null, string? defaultRoot = null)
    {
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
            ScanOutcome outcome = await Task.Run(() => Collect(root, query, cts.Token), CancellationToken.None);

            _dispatcher.Post(() =>
            {
                foreach (MediaFile file in outcome.Files)
                    Results.Add(MediaItem.From(file));

                StatusMessage = outcome.Cancelled
                    ? $"Остановлено, найдено: {outcome.Files.Count}"
                    : outcome.Files.Count >= ResultLimit
                        ? $"Показаны первые {ResultLimit} — уточните фильтры"
                        : $"Найдено: {outcome.Files.Count}";
            });
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

    [RelayCommand]
    private void Stop() => _scan?.Cancel();

    /// <summary>
    /// Обойти дерево в фоновом потоке. Отмена не теряет уже найденное: пользователь
    /// нажал «Стоп», потому что увидел достаточно, — показываем это.
    /// </summary>
    private ScanOutcome Collect(string root, MediaQuery query, CancellationToken cancellationToken)
    {
        var found = new List<MediaFile>();
        bool cancelled = false;
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
        return new ScanOutcome(found, cancelled);
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
            MaxResults = ResultLimit,
        };
    }
}

/// <summary>Итог одного прохода поиска.</summary>
/// <param name="Files">Найденные файлы (в том числе частично, если отменили).</param>
/// <param name="Cancelled">Прервал ли пользователь поиск.</param>
internal sealed record ScanOutcome(List<MediaFile> Files, bool Cancelled);

/// <summary>Строка списка найденного медиа.</summary>
public sealed record MediaItem(string FileName, string FullPath, string Kind, string Size, string ModifiedAt)
{
    public static MediaItem From(MediaFile file) => new(
        file.FileName,
        file.FullPath,
        file.Kind == MediaKind.Photo ? "Фото" : "Видео",
        FormatSize(file.Size),
        file.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Б",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} КБ",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} МБ",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} ГБ",
    };
}
