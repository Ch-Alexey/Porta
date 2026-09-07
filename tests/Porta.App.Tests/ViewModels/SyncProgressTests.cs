using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Porta.App.ViewModels;
using Porta.Core.Discovery;
using Porta.Core.Identity;
using Porta.Core.Sync;

namespace Porta.App.Tests.ViewModels;

/// <summary>Прогресс и отмена синхронизации. См. docs/features/33-progress-and-cancel.md.</summary>
public class SyncProgressTests
{
    private static DiscoveredPeer Peer(DeviceId id)
        => new(id, "Ноутбук", [new IPEndPoint(IPAddress.Loopback, 7000)], DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Sync_shows_progress_while_running_and_clears_it_after()
    {
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var sync = new FakeSyncController();
        sync.Reports.Add(new TransferProgress(0, 0, 1024 * 1024, 4 * 1024 * 1024));

        var vm = new DevicesViewModel(new FakeAppData(), discovery, new ImmediateDispatcher(), sync);
        var seen = new List<string?>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SyncProgressText))
                seen.Add(vm.SyncProgressText);
        };
        discovery.RaiseDiscovered(Peer(id.Id));

        await vm.SyncWithFoundCommand.ExecuteAsync(null);

        Assert.Contains(seen, t => t is not null && t.Contains("из 4 МБ"));
        Assert.Null(vm.SyncProgressText);
        Assert.False(vm.IsSyncing);
    }

    [Fact]
    public async Task Cancelling_sync_is_reported_and_not_treated_as_an_error()
    {
        using var id = DeviceIdentity.Generate();
        var discovery = new FakeDeviceDiscovery();
        var sync = new CancellingSync();
        var vm = new DevicesViewModel(new FakeAppData(), discovery, new ImmediateDispatcher(), sync);
        discovery.RaiseDiscovered(Peer(id.Id));

        sync.CancelWhenCalled = vm;
        await vm.SyncWithFoundCommand.ExecuteAsync(null);

        Assert.StartsWith("Синхронизация отменена", vm.StatusMessage);
        Assert.False(vm.IsSyncing);
    }

    /// <summary>Отменяет синхронизацию изнутри — как если бы человек нажал «Отменить».</summary>
    private sealed class CancellingSync : ISyncController
    {
        public DevicesViewModel? CancelWhenCalled { get; set; }

        public Task<string> SyncWithPeerAsync(
            DiscoveredPeer peer,
            SyncTrigger trigger = SyncTrigger.Manual,
            IProgress<TransferProgress>? progress = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            CancelWhenCalled?.CancelSyncCommand.Execute(null);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("ok");
        }
    }
}
