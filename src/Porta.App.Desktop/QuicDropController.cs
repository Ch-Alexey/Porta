using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Discovery;
using Porta.Core.Drop;
using Porta.Core.Sync;
using Porta.Infrastructure.Transport;

namespace Porta.App.Desktop;

/// <summary>
/// Разовая отправка файлов поверх QUIC. См. docs/features/28-drop-ui.md.
/// </summary>
public sealed class QuicDropController(AppEnvironment environment) : IDropController
{
    public async Task<DropSendResult> SendAsync(
        DiscoveredPeer peer,
        IReadOnlyList<DropSourceFile> files,
        CancellationToken cancellationToken = default)
    {
        if (!QuicTransport.IsSupported)
            return new DropSendResult(false, 0, 0, "QUIC недоступен (нет libmsquic)");

        IPEndPoint? endpoint = SelectEndpoint(peer);
        if (endpoint is null)
            return new DropSendResult(false, 0, 0, "У устройства нет доступного адреса");

        using var transport = new QuicTransport(environment.Identity);
        var service = new SyncService(
            transport, environment.Identity, environment.DeviceName,
            new DeviceRepositoryTrustPolicy(environment.Devices), environment.FileIndex);

        return await service.SendFilesAsync(endpoint, peer.DeviceId, files, cancellationToken).ConfigureAwait(false);
    }

    private static IPEndPoint? SelectEndpoint(DiscoveredPeer peer)
        => peer.Endpoints.FirstOrDefault(e => e.Address.AddressFamily == AddressFamily.InterNetwork)
           ?? peer.Endpoints.FirstOrDefault();
}
