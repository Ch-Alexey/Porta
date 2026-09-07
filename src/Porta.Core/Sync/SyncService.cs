using System.Net;
using Porta.Core.Data;
using Porta.Core.Drop;
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
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using IPeerConnection connection = await _transport.ConnectAsync(endpoint, peer, cancellationToken).ConfigureAwait(false);
        await using PeerSession session = await PeerSession.EstablishAsync(
            connection, _trust, _selfName, isInitiator: true,
            SessionIntentKind.Sync, cancellationToken).ConfigureAwait(false);

        return await VersionedSync.PullAsync(
            session.Control, _index, folder, storageId, _self.Id, peer, versions, ignore,
            progress: progress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Инициатор: подключиться к устройству и разово передать файлы.</summary>
    public async Task<DropSendResult> SendFilesAsync(
        IPEndPoint endpoint,
        DeviceId peer,
        IReadOnlyList<DropSourceFile> files,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using IPeerConnection connection = await _transport.ConnectAsync(endpoint, peer, cancellationToken).ConfigureAwait(false);
        await using PeerSession session = await PeerSession.EstablishAsync(
            connection, _trust, _selfName, isInitiator: true,
            SessionIntentKind.Drop, cancellationToken).ConfigureAwait(false);

        return await DropProtocol.SendAsync(
            session.Control, files, transferId: null, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Отдающая сторона: принять одно соединение и обслужить его по объявленному
    /// намерению — синк хранилища или приём разовой передачи.
    /// Папку хранилища определяет <paramref name="resolveFolder"/> (storageId → путь,
    /// null — нет доступа). Если <paramref name="drops"/> не задан, передачи отклоняются.
    /// </summary>
    public async Task ServeOnceAsync(
        ITransportListener listener,
        Func<string, string?> resolveFolder,
        IgnoreRules? ignore = null,
        IDropAcceptance? drops = null,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using IPeerConnection connection = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
        await using PeerSession session = await PeerSession.EstablishAsync(
            connection, _trust, _selfName, isInitiator: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (session.Intent == SessionIntentKind.Drop)
        {
            await DropProtocol.ReceiveAsync(
                session.Control, drops ?? RejectingAcceptance.Instance, session.RemoteDeviceId,
                progress, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await VersionedSync.ServeAsync(
            session.Control, _index, resolveFolder, _self.Id, ignore, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Приём не настроен — вежливо отказываем, а не рвём соединение.</summary>
    private sealed class RejectingAcceptance : IDropAcceptance
    {
        public static RejectingAcceptance Instance { get; } = new();

        public Task<DropDecision> DecideAsync(DropOfferMessage offer, DeviceId sender, CancellationToken cancellationToken = default)
            => Task.FromResult(DropDecision.Reject("Приём разовых передач не настроен"));
    }
}
