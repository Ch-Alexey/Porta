using System.Net;
using Porta.Core.Data;
using Porta.Core.Identity;
using Porta.Core.Indexing;
using Porta.Core.Protocol;
using Porta.Core.Transport;

namespace Porta.Core.Sync;

/// <summary>
/// Прикладной оркестратор синхронизации: соединяет транспорт, сессию, доверие и
/// версионированный синк. Не зависит от конкретного транспорта (через <see cref="ITransport"/>).
/// См. docs/features/19-sync-service.md.
/// </summary>
public sealed class SyncService
{
    private readonly ITransport _transport;
    private readonly DeviceIdentity _self;
    private readonly string _selfName;
    private readonly ITrustPolicy _trust;
    private readonly FileIndexRepository _index;

    public SyncService(
        ITransport transport,
        DeviceIdentity self,
        string selfName,
        ITrustPolicy trust,
        FileIndexRepository index)
    {
        _transport = transport;
        _self = self;
        _selfName = selfName;
        _trust = trust;
        _index = index;
    }

    /// <summary>Инициатор: подключиться к устройству и подтянуть хранилище.</summary>
    public async Task<SyncApplyReport> PullAsync(
        IPEndPoint endpoint,
        DeviceId peer,
        string storageId,
        string folder,
        IVersionStore? versions = null,
        IgnoreRules? ignore = null,
        CancellationToken cancellationToken = default)
    {
        await using IPeerConnection connection = await _transport.ConnectAsync(endpoint, peer, cancellationToken).ConfigureAwait(false);
        await using PeerSession session = await PeerSession.EstablishAsync(
            connection, _trust, _selfName, isInitiator: true, cancellationToken).ConfigureAwait(false);

        return await VersionedSync.PullAsync(
            session.Control, _index, folder, storageId, _self.Id, peer, versions, ignore, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Отдающая сторона: принять одно соединение и отдать запрошенное хранилище.
    /// Папку определяет <paramref name="resolveFolder"/> (storageId → путь, null — нет доступа).
    /// </summary>
    public async Task ServeOnceAsync(
        ITransportListener listener,
        Func<string, string?> resolveFolder,
        IgnoreRules? ignore = null,
        CancellationToken cancellationToken = default)
    {
        await using IPeerConnection connection = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
        await using PeerSession session = await PeerSession.EstablishAsync(
            connection, _trust, _selfName, isInitiator: false, cancellationToken).ConfigureAwait(false);

        await VersionedSync.ServeAsync(
            session.Control, _index, resolveFolder, _self.Id, ignore, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
