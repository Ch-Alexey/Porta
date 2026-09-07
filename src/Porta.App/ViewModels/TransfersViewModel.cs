using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.App.Services;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Drop;
using Porta.Core.Sync;

namespace Porta.App.ViewModels;

/// <summary>
/// Вкладка «Передача»: разово отправить файлы найденному устройству и подтвердить
/// приём входящих. См. docs/features/28-drop-ui.md.
/// </summary>
public partial class TransfersViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settings;
    private readonly IDropController? _drops;
    private readonly IFilePicker? _picker;
    private readonly IFolderPicker? _folders;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, DiscoveredPeer> _peers = new(StringComparer.Ordinal);
    private readonly Dictionary<PendingDropOffer, IncomingOfferItem> _incoming = [];
    private System.Threading.CancellationTokenSource? _sending;

    public TransfersViewModel(
        ISettingsRepository settings,
        IDropController? drops = null,
        IFilePicker? picker = null,
        IDeviceDiscovery? discovery = null,
        UiDropAcceptance? acceptance = null,
        IUiDispatcher? dispatcher = null,
        IFolderPicker? folders = null)
    {
        _settings = settings;
        _drops = drops;
        _picker = picker;
        _folders = folders;
        _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
        DownloadsFolder = settings.Get(SettingKeys.DownloadsFolder, DefaultDownloadsFolder());

        if (discovery is not null)
        {
            discovery.PeerDiscovered += OnPeerDiscovered;
            discovery.PeerLost += OnPeerLost;
        }

        if (acceptance is not null)
        {
            acceptance.OfferReceived += OnOfferReceived;
            acceptance.OfferClosed += OnOfferClosed;
            acceptance.ProgressChanged += p => _dispatcher.Post(() => OnIncomingProgress(p));
        }
    }

    /// <summary>Куда по умолчанию складывать принятое — отдельно от браузерных загрузок.</summary>
    public static string DefaultDownloadsFolder()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Porta");

    /// <summary>Файлы, выбранные к отправке.</summary>
    public ObservableCollection<OutgoingFileItem> SelectedFiles { get; } = [];

    /// <summary>Устройства, найденные в сети.</summary>
    public ObservableCollection<PeerChoice> Peers { get; } = [];

    /// <summary>Входящие предложения, ждущие решения.</summary>
    public ObservableCollection<IncomingOfferItem> IncomingOffers { get; } = [];

    [ObservableProperty]
    public partial PeerChoice? SelectedPeer { get; set; }

    [ObservableProperty]
    public partial string DownloadsFolder { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSending { get; set; }

    /// <summary>Ход отправки: «12 из 40 файлов, 340 МБ из 1.2 ГБ».</summary>
    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    /// <summary>Доля выполненного 0..1 — для полосы прогресса.</summary>
    [ObservableProperty]
    public partial double ProgressFraction { get; set; }

    /// <summary>Ход приёма входящей передачи.</summary>
    [ObservableProperty]
    public partial string? IncomingProgressText { get; set; }

    private void OnIncomingProgress(TransferProgress p)
    {
        // Приём закончился — убираем строку, иначе она врёт про идущую работу.
        IncomingProgressText = p.FilesTotal > 0 && p.FilesDone >= p.FilesTotal
            ? null
            : $"Приём: {p.FilesDone} из {p.FilesTotal} · {FormatSize(p.BytesDone)} из {FormatSize(p.BytesTotal)}";
    }

    /// <summary>Суммарный объём выбранного — человеку полезно до отправки.</summary>
    public string SelectionSummary => SelectedFiles.Count == 0
        ? "Файлы не выбраны"
        : $"Выбрано файлов: {SelectedFiles.Count}, объём: {FormatSize(SelectedFiles.Sum(f => f.Size))}";

    [RelayCommand]
    private async Task PickFilesAsync()
    {
        if (_picker is null)
            return;

        IReadOnlyList<string> picked = await _picker.PickFilesAsync("Выберите файлы для отправки");
        foreach (string path in picked)
        {
            if (SelectedFiles.Any(f => string.Equals(f.FullPath, path, StringComparison.Ordinal)))
                continue;
            SelectedFiles.Add(OutgoingFileItem.From(path));
        }

        OnPropertyChanged(nameof(SelectionSummary));
    }

    /// <summary>Добавить файлы к отправке (например, найденные во вкладке «Медиа»).</summary>
    public void AddFiles(IReadOnlyList<string> paths)
    {
        foreach (string path in paths)
        {
            if (SelectedFiles.Any(f => string.Equals(f.FullPath, path, StringComparison.Ordinal)))
                continue;
            SelectedFiles.Add(OutgoingFileItem.From(path));
        }

        OnPropertyChanged(nameof(SelectionSummary));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedFiles.Clear();
        OnPropertyChanged(nameof(SelectionSummary));
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsSending)
            return;
        if (_drops is null)
        {
            StatusMessage = "Отправка недоступна";
            return;
        }
        if (SelectedFiles.Count == 0)
        {
            StatusMessage = "Сначала выберите файлы";
            return;
        }
        if (SelectedPeer is null || !_peers.TryGetValue(SelectedPeer.DeviceId, out DiscoveredPeer? peer))
        {
            StatusMessage = "Выберите устройство";
            return;
        }

        var files = SelectedFiles.Select(f => DropSourceFile.FromPath(f.FullPath)).ToList();
        IsSending = true;
        StatusMessage = "Отправка…";
        ProgressText = null;
        ProgressFraction = 0;

        using var cts = new System.Threading.CancellationTokenSource();
        _sending = cts;
        var progress = new DispatchedProgress<TransferProgress>(_dispatcher, OnProgress);
        try
        {
            DropSendResult result = await _drops.SendAsync(peer, files, progress, cts.Token);
            StatusMessage = result.Accepted
                ? $"Отправлено файлов: {result.FilesSent} ({FormatSize(result.BytesSent)})"
                : $"Отклонено: {result.RejectReason}";

            if (result.Accepted)
                ClearSelection();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Отправка отменена";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка отправки: " + ex.Message;
        }
        finally
        {
            _sending = null;
            IsSending = false;
            ProgressText = null;
            ProgressFraction = 0;
        }
    }

    /// <summary>Прервать идущую отправку.</summary>
    [RelayCommand]
    private void CancelSend() => _sending?.Cancel();

    private void OnProgress(TransferProgress p)
    {
        ProgressText = p.FilesTotal > 1
            ? $"{p.FilesDone} из {p.FilesTotal} файлов · {FormatSize(p.BytesDone)} из {FormatSize(p.BytesTotal)}"
            : $"{FormatSize(p.BytesDone)} из {FormatSize(p.BytesTotal)}";
        ProgressFraction = p.Fraction ?? 0;
    }

    [RelayCommand]
    private async Task BrowseDownloadsAsync()
    {
        if (_folders is null)
            return;

        string? picked = await _folders.PickFolderAsync("Куда складывать принятые файлы");
        if (!string.IsNullOrEmpty(picked))
            DownloadsFolder = picked;
    }

    [RelayCommand]
    private void SaveDownloadsFolder()
    {
        if (string.IsNullOrWhiteSpace(DownloadsFolder))
        {
            StatusMessage = "Укажите папку для принятых файлов";
            return;
        }

        string folder = DownloadsFolder.Trim();
        try
        {
            // Проверяем сразу, а не в момент приёма через полчаса.
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось использовать папку: {ex.Message}";
            return;
        }

        _settings.Set(SettingKeys.DownloadsFolder, folder);
        StatusMessage = "Папка для приёма сохранена";
    }

    /// <summary>Актуальная папка приёма — читается ядром в момент решения.</summary>
    public string CurrentDownloadsFolder()
        => _settings.Get(SettingKeys.DownloadsFolder, DefaultDownloadsFolder());

    private void Accept(IncomingOfferItem item)
    {
        item.Pending.Accept();
        StatusMessage = $"Принимаем файлы от «{item.SenderName}»";
    }

    private void Reject(IncomingOfferItem item)
    {
        item.Pending.Reject();
        StatusMessage = $"Передача от «{item.SenderName}» отклонена";
    }

    private void OnOfferReceived(PendingDropOffer pending) => _dispatcher.Post(() =>
    {
        string name = _peers.TryGetValue(pending.Sender.ToString(), out DiscoveredPeer? peer)
            ? peer.Name
            : pending.Sender.ToDisplayString();

        var item = new IncomingOfferItem(pending, name, Accept, Reject);
        _incoming[pending] = item;
        IncomingOffers.Add(item);
    });

    private void OnOfferClosed(PendingDropOffer pending) => _dispatcher.Post(() =>
    {
        if (!_incoming.Remove(pending, out IncomingOfferItem? item))
            return;
        IncomingOffers.Remove(item);
    });

    private void OnPeerDiscovered(DiscoveredPeer peer) => _dispatcher.Post(() =>
    {
        string id = peer.DeviceId.ToString();
        if (!_peers.TryAdd(id, peer))
            return;
        Peers.Add(new PeerChoice(peer.Name, id));
    });

    private void OnPeerLost(Porta.Core.Identity.DeviceId deviceId) => _dispatcher.Post(() =>
    {
        string id = deviceId.ToString();
        if (!_peers.Remove(id))
            return;

        PeerChoice? existing = Peers.FirstOrDefault(p => p.DeviceId == id);
        if (existing is not null)
            Peers.Remove(existing);
    });

    internal static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Б",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} КБ",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} МБ",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} ГБ",
    };
}

/// <summary>Файл, выбранный к отправке.</summary>
public sealed record OutgoingFileItem(string FullPath, string FileName, long Size, string SizeText)
{
    public static OutgoingFileItem From(string path)
    {
        long size = 0;
        try
        {
            size = new FileInfo(path).Length;
        }
        catch (IOException)
        {
            // Размер не прочитался — покажем 0, отправка всё равно упрётся в реальный файл.
        }

        return new OutgoingFileItem(path, Path.GetFileName(path), size, TransfersViewModel.FormatSize(size));
    }
}

/// <summary>Найденное устройство для выбора получателя.</summary>
public sealed record PeerChoice(string Name, string DeviceId)
{
    public override string ToString() => Name;
}

/// <summary>
/// Входящее предложение в списке. Команды живут на самом элементе: шаблону не нужно
/// искать view-модель через предка, и решение нельзя случайно применить не к тому.
/// </summary>
public sealed class IncomingOfferItem
{
    internal IncomingOfferItem(
        PendingDropOffer pending,
        string senderName,
        Action<IncomingOfferItem> accept,
        Action<IncomingOfferItem> reject)
    {
        Pending = pending;
        SenderName = senderName;
        AcceptCommand = new RelayCommand(() => accept(this));
        RejectCommand = new RelayCommand(() => reject(this));
    }

    internal PendingDropOffer Pending { get; }

    public string SenderName { get; }

    public string Summary =>
        $"{Pending.FileCount} файл(ов), {TransfersViewModel.FormatSize(Pending.TotalSize)}";

    public ICommand AcceptCommand { get; }

    public ICommand RejectCommand { get; }
}
