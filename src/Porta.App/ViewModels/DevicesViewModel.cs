using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porta.App.Services;
using Porta.Core.App;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Model;

namespace Porta.App.ViewModels;

/// <summary>
/// Вкладка «Устройства»: доверенные устройства, найденные в сети (mDNS) и связывание по токену.
/// </summary>
public partial class DevicesViewModel : ViewModelBase
{
    private readonly IAppData _data;
    private readonly IUiDispatcher _dispatcher;

    public DevicesViewModel(IAppData data, IDeviceDiscovery? discovery = null, IUiDispatcher? dispatcher = null)
    {
        _data = data;
        _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
        Reload();

        if (discovery is not null)
        {
            discovery.PeerDiscovered += OnPeerDiscovered;
            discovery.PeerLost += OnPeerLost;
        }
    }

    public ObservableCollection<DeviceItem> Items { get; } = [];

    /// <summary>Устройства, найденные в локальной сети (ещё не обязательно доверенные).</summary>
    public ObservableCollection<DiscoveredPeerItem> DiscoveredPeers { get; } = [];

    /// <summary>Токен приглашения этого устройства (для QR/копирования).</summary>
    [ObservableProperty]
    public partial string InvitationToken { get; set; } = string.Empty;

    /// <summary>Вставленный токен другого устройства.</summary>
    [ObservableProperty]
    public partial string PastedToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewDeviceName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [RelayCommand]
    private void GenerateInvitation() => InvitationToken = _data.CreateInvitation();

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

    private void OnPeerDiscovered(DiscoveredPeer peer) => _dispatcher.Post(() =>
    {
        string id = peer.DeviceId.ToString();
        if (DiscoveredPeers.All(p => p.DeviceId != id))
            DiscoveredPeers.Add(new DiscoveredPeerItem(peer.Name, id));
    });

    private void OnPeerLost(DeviceId deviceId) => _dispatcher.Post(() =>
    {
        string id = deviceId.ToString();
        DiscoveredPeerItem? existing = DiscoveredPeers.FirstOrDefault(p => p.DeviceId == id);
        if (existing is not null)
            DiscoveredPeers.Remove(existing);
    });

    private void Reload()
    {
        Items.Clear();
        foreach (TrustedDevice device in _data.Devices.List())
            Items.Add(new DeviceItem(device.Name, device.Id.ToDisplayString()));
    }
}

/// <summary>Строка списка доверенных устройств для отображения.</summary>
public sealed record DeviceItem(string Name, string DeviceId);

/// <summary>Строка списка найденных в сети устройств.</summary>
public sealed record DiscoveredPeerItem(string Name, string DeviceId);
