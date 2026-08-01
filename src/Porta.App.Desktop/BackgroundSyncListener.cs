using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Porta.Core.App;
using Porta.Core.Data;
using Porta.Core.Sync;
using Porta.Core.Transport;
using Porta.Infrastructure.Transport;

namespace Porta.App.Desktop;

/// <summary>
/// Фоновый приём входящих синхронизаций: слушает QUIC на заданном порту и отдаёт
/// запрошенные хранилища доверенным устройствам. См. docs/features/21-sync-from-ui.md.
/// </summary>
public sealed class BackgroundSyncListener(AppEnvironment environment, int port) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private QuicTransport? _transport;
    private Task? _loop;

    public void Start()
    {
        if (!QuicTransport.IsSupported)
            return;
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _transport = new QuicTransport(environment.Identity);
            await using ITransportListener listener =
                await _transport.ListenAsync(new IPEndPoint(IPAddress.Any, port), cancellationToken).ConfigureAwait(false);

            var service = new SyncService(
                _transport, environment.Identity, environment.DeviceName,
                new DeviceRepositoryTrustPolicy(environment.Devices), environment.FileIndex);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await service.ServeOnceAsync(listener, ResolveFolder, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Одно соединение упало (недоверенный пир, обрыв) — продолжаем принимать.
                }
            }
        }
        catch (Exception)
        {
            // Слушатель не поднялся (порт занят, нет прав) — приём просто не работает.
        }
    }

    private string? ResolveFolder(string storageId) => environment.Storages.Get(storageId)?.LocalPath;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch { /* игнорируем при завершении */ }
        }
        _transport?.Dispose();
        _cts.Dispose();
    }
}
