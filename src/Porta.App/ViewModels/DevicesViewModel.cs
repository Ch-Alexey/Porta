using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Model;
using Porta.Core.Pairing;
using Porta.Core.Sync;
using Porta.App.Services;

namespace Porta.App.ViewModels;

/// <summary>
/// Вкладка «Устройства»: доверенные устройства, найденные в сети (mDNS), связывание по
/// токену и запуск синхронизации с найденными.
/// </summary>
public partial class DevicesViewModel : ViewModelBase
{
    private readonly IAppData _data;
    private readonly IUiDispatcher _dispatcher;
    private readonly ISyncController? _sync;
    private readonly IQrCodeRenderer? _qr;
    private readonly Dictionary<string, DiscoveredPeer> _peers = new(StringComparer.Ordinal);
    private CancellationTokenSource? _syncing;
    private readonly TimeProvider _clock;
    private DateTimeOffset? _invitationExpiresAt;
    private readonly PeerOperations? _operations;

    public DevicesViewModel(
        IAppData data,
        IDeviceDiscovery? discovery = null,
        IUiDispatcher? dispatcher = null,
        ISyncController? sync = null,
        IQrCodeRenderer? qr = null,
        TimeProvider? clock = null,
        PeerOperations? operations = null)
    {
        _data = data;
        _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
        _sync = sync;
        _qr = qr;
        _clock = clock ?? TimeProvider.System;
        _operations = operations;
        Reload();

        if (discovery is not null)
        {
            discovery.PeerDiscovered += OnPeerDiscovered;
            discovery.PeerLost += OnPeerLost;
        }
    }

    /// <summary>Есть ли найденные устройства для синхронизации.</summary>
    public bool CanSync => _sync is not null && DiscoveredPeers.Count > 0;

    public ObservableCollection<DeviceItem> Items { get; } = [];

    /// <summary>Устройства, найденные в локальной сети (ещё не обязательно доверенные).</summary>
    public ObservableCollection<DiscoveredPeerItem> DiscoveredPeers { get; } = [];

    /// <summary>Токен приглашения этого устройства (для QR/копирования).</summary>
    [ObservableProperty]
    public partial string InvitationToken { get; set; } = string.Empty;

    /// <summary>
    /// Тот же токен картинкой (PNG). Байты, а не готовый <c>Bitmap</c>: view-модель
    /// остаётся тестируемой без графической подсистемы, картинку собирает конвертер
    /// во view-слое. См. docs/features/30-qr-pairing.md.
    /// </summary>
    [ObservableProperty]
    public partial byte[]? InvitationQrPng { get; set; }

    /// <summary>Вставленный токен другого устройства.</summary>
    [ObservableProperty]
    public partial string PastedToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewDeviceName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Идёт ли синхронизация — для кнопки отмены.</summary>
    [ObservableProperty]
    public partial bool IsSyncing { get; set; }

    /// <summary>Ход синхронизации в байтах.</summary>
    [ObservableProperty]
    public partial string? SyncProgressText { get; set; }

    /// <summary>До какого момента приглашение действительно.</summary>
    [ObservableProperty]
    public partial string? InvitationExpiryText { get; set; }

    /// <summary>
    /// Отозвать доверие: ключ и связи с хранилищами уходят, файлы на диске остаются.
    /// См. docs/features/29-managing-what-exists.md.
    /// </summary>
    private void Revoke(DeviceItem item)
    {
        // DeviceId.Parse выбрасывает дефисы, поэтому читаемая форма разбирается как есть.
        DeviceId id = DeviceId.Parse(item.DeviceId);
        _data.Devices.Remove(id);

        // Идущая операция с этим устройством должна прерваться, а не доработать до конца.
        bool interrupted = _operations?.CancelFor(id) ?? false;
        StatusMessage = interrupted
            ? $"Доверие к «{item.Name}» отозвано, текущая операция прервана. Полученные файлы остались."
            : $"Доверие к «{item.Name}» отозвано. Полученные файлы остались.";
        Reload();
    }

    [RelayCommand]
    private void GenerateInvitation()
    {
        InvitationToken = _data.CreateInvitation();
        InvitationQrPng = RenderQr(InvitationToken);
        _invitationExpiresAt = ExpiryOf(InvitationToken);
        InvitationExpiryText = _invitationExpiresAt is { } until
            ? $"Действительно до {until.ToLocalTime():HH:mm:ss}"
            : null;
    }

    /// <summary>
    /// Убрать приглашение, если срок вышел. Зовётся при обращении к вкладке и перед
    /// показом — крутить фоновый таймер ради надписи незачем.
    /// См. docs/features/34-ui-polish.md.
    /// </summary>
    public void DropExpiredInvitation()
    {
        if (_invitationExpiresAt is not { } until || _clock.GetUtcNow() < until)
            return;

        InvitationToken = string.Empty;
        InvitationQrPng = null;
        InvitationExpiryText = null;
        _invitationExpiresAt = null;
        StatusMessage = "Приглашение истекло — создайте новое";
    }

    /// <summary>Срок действия токена; null — токен не разобрать.</summary>
    private static DateTimeOffset? ExpiryOf(string token)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(PairingTokenCodec.Decode(token).ExpiresAtUnix);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Убрать приглашение с экрана — оно одноразовое и не должно висеть вечно.</summary>
    [RelayCommand]
    private void HideInvitation()
    {
        InvitationToken = string.Empty;
        InvitationQrPng = null;
        InvitationExpiryText = null;
        _invitationExpiresAt = null;
    }

    private byte[]? RenderQr(string token)
    {
        if (_qr is null || string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            return _qr.RenderPng(token);
        }
        catch (Exception)
        {
            // Без картинки токен всё равно можно скопировать строкой — не роняем вкладку.
            return null;
        }
    }

    [RelayCommand]
    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(PastedToken))
            return;

        try
        {
            TrustedDevice device = _data.AcceptInvitation(PastedToken.Trim(), NewDeviceName);
            PastedToken = string.Empty;
            NewDeviceName = string.Empty;
            StatusMessage = $"Устройство «{device.Name}» добавлено";
            Reload();
        }
        catch (FormatException)
        {
            StatusMessage = "Некорректный токен связывания";
        }
    }

    [RelayCommand]
    private async Task SyncWithFoundAsync()
    {
        if (_sync is null || _peers.Count == 0 || IsSyncing)
            return;

        StatusMessage = "Синхронизация…";
        IsSyncing = true;
        int devices = 0;

        using var cts = new CancellationTokenSource();
        _syncing = cts;
        var progress = new DispatchedProgress<TransferProgress>(
            _dispatcher,
            p => SyncProgressText = p.BytesTotal > 0
                ? $"{FormatSize(p.BytesDone)} из {FormatSize(p.BytesTotal)}"
                : null);
        try
        {
            foreach (DiscoveredPeer peer in _peers.Values.ToList())
            {
                await _sync.SyncWithPeerAsync(peer, SyncTrigger.Manual, progress, cts.Token);
                devices++;
            }
            StatusMessage = $"Синхронизировано с {devices} устр.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Синхронизация отменена (успели: {devices} устр.)";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка синхронизации: " + ex.Message;
        }
        finally
        {
            _syncing = null;
            IsSyncing = false;
            SyncProgressText = null;
        }
    }

    /// <summary>Прервать идущую синхронизацию.</summary>
    [RelayCommand]
    private void CancelSync() => _syncing?.Cancel();

    private static string FormatSize(long bytes) => TransfersViewModel.FormatSize(bytes);

    private void OnPeerDiscovered(DiscoveredPeer peer) => _dispatcher.Post(() =>
    {
        string id = peer.DeviceId.ToString();
        _peers[id] = peer;
        if (DiscoveredPeers.All(p => p.DeviceId != id))
            DiscoveredPeers.Add(new DiscoveredPeerItem(peer.Name, id));
        OnPropertyChanged(nameof(CanSync));
    });

    private void OnPeerLost(DeviceId deviceId) => _dispatcher.Post(() =>
    {
        string id = deviceId.ToString();
        _peers.Remove(id);
        DiscoveredPeerItem? existing = DiscoveredPeers.FirstOrDefault(p => p.DeviceId == id);
        if (existing is not null)
            DiscoveredPeers.Remove(existing);
        OnPropertyChanged(nameof(CanSync));
    });

    private void Reload()
    {
        Items.Clear();
        foreach (TrustedDevice device in _data.Devices.List())
            Items.Add(new DeviceItem(device.Name, device.Id.ToDisplayString(), Revoke));
    }
}

/// <summary>Строка списка доверенных устройств для отображения.</summary>
/// <summary>
/// Доверенное устройство в списке. Команда живёт на самой строке — шаблону не нужно
/// искать view-модель через предка.
/// </summary>
public sealed class DeviceItem
{
    internal DeviceItem(string name, string deviceId, Action<DeviceItem> revoke)
    {
        Name = name;
        DeviceId = deviceId;
        RevokeCommand = new RelayCommand(() => revoke(this));
    }

    public string Name { get; }

    /// <summary>Device ID в читаемой форме (группами через дефис).</summary>
    public string DeviceId { get; }

    public ICommand RevokeCommand { get; }
}

/// <summary>Строка списка найденных в сети устройств.</summary>
public sealed record DiscoveredPeerItem(string Name, string DeviceId);
