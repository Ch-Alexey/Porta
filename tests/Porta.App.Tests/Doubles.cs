using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Porta.App.Services;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Sync;

namespace Porta.App.Tests;

/// <summary>Диспетчер, выполняющий действие немедленно (для тестов без UI-потока).</summary>
internal sealed class ImmediateDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Управляемое обнаружение устройств: тест сам поднимает события.</summary>
internal sealed class FakeDeviceDiscovery : IDeviceDiscovery
{
    public event Action<DiscoveredPeer>? PeerDiscovered;
    public event Action<DeviceId>? PeerLost;

    public void Advertise(PortaAdvertisement self) { }
    public void StartBrowsing() { }
    public void Dispose() { }

    public void RaiseDiscovered(DiscoveredPeer peer) => PeerDiscovered?.Invoke(peer);
    public void RaiseLost(DeviceId deviceId) => PeerLost?.Invoke(deviceId);
}

/// <summary>Контроллер синка, записывающий вызовы (для тестов VM).</summary>
internal sealed class FakeSyncController : ISyncController
{
    public List<DiscoveredPeer> Calls { get; } = [];

    public Task<string> SyncWithPeerAsync(DiscoveredPeer peer, CancellationToken cancellationToken = default)
    {
        Calls.Add(peer);
        return Task.FromResult("ok");
    }
}

/// <summary>Настройки в памяти — для тестов view-моделей без БД.</summary>
internal sealed class InMemorySettings : Porta.Core.Data.ISettingsRepository
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string Get(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

    public void Set(string key, string value) => _values[key] = value;
}

/// <summary>Отправка, записывающая вызовы и возвращающая заданный результат.</summary>
internal sealed class FakeDropController(Porta.Core.Drop.DropSendResult? result = null)
    : Porta.Core.Drop.IDropController
{
    public List<(DiscoveredPeer Peer, IReadOnlyList<Porta.Core.Drop.DropSourceFile> Files)> Calls { get; } = [];

    public Exception? Throws { get; set; }

    public Task<Porta.Core.Drop.DropSendResult> SendAsync(
        DiscoveredPeer peer,
        IReadOnlyList<Porta.Core.Drop.DropSourceFile> files,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((peer, files));
        if (Throws is not null)
            return Task.FromException<Porta.Core.Drop.DropSendResult>(Throws);
        return Task.FromResult(result ?? new Porta.Core.Drop.DropSendResult(true, files.Count, 1024));
    }
}

/// <summary>Выбор файлов, отдающий заранее заданный список.</summary>
internal sealed class FakeFilePicker(params string[] paths) : Porta.App.Services.IFilePicker
{
    public Task<IReadOnlyList<string>> PickFilesAsync(string title)
        => Task.FromResult<IReadOnlyList<string>>(paths);
}
