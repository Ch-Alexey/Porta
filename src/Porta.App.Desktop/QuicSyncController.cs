using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Model;
using Porta.Core.Sync;
using Porta.Infrastructure.Transport;

namespace Porta.App.Desktop;

/// <summary>
/// Реализация синхронизации из UI поверх QUIC: подключается к найденному устройству и
/// тянет все локальные хранилища. См. docs/features/21-sync-from-ui.md.
/// </summary>
public sealed class QuicSyncController(AppEnvironment environment) : ISyncController
{
    public async Task<string> SyncWithPeerAsync(DiscoveredPeer peer, CancellationToken cancellationToken = default)
    {
        if (!QuicTransport.IsSupported)
            return "QUIC недоступен (нет libmsquic)";

        IPEndPoint? endpoint = SelectEndpoint(peer);
        if (endpoint is null)
            return "У устройства нет доступного адреса";

        using var transport = new QuicTransport(environment.Identity);
        var service = new SyncService(
            transport, environment.Identity, environment.DeviceName,
            new DeviceRepositoryTrustPolicy(environment.Devices), environment.FileIndex);

        int totalFiles = 0;
        foreach (Storage storage in StorageSharing.SharedWith(environment.Storages, peer.DeviceId))
        {
            SyncApplyReport report = await service
                .PullAsync(endpoint, peer.DeviceId, storage.Id, storage.LocalPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            totalFiles += report.Accepted;
        }

        return $"обновлено файлов: {totalFiles}";
    }

    private static IPEndPoint? SelectEndpoint(DiscoveredPeer peer)
        => peer.Endpoints.FirstOrDefault(e => e.Address.AddressFamily == AddressFamily.InterNetwork)
           ?? peer.Endpoints.FirstOrDefault();
}
