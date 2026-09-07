using System.Collections.Concurrent;
using Porta.Core.Identity;

namespace Porta.Core.Sync;

/// <summary>
/// Учёт идущих операций по устройствам, чтобы их можно было оборвать при отзыве доверия.
/// Доверие проверяется при установке сессии, но уже идущую сессию надо прерывать явно.
/// См. docs/features/34-ui-polish.md.
/// </summary>
public sealed class PeerOperations : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// Начать операцию с устройством. Возвращённый токен отменяется при
    /// <see cref="CancelFor"/>; область освобождает регистрацию.
    /// </summary>
    public Operation Begin(DeviceId peer, CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string key = peer.ToString();

        // Одно устройство — одна запись; предыдущую (если осталась) не теряем, а заменяем.
        _running.AddOrUpdate(key, cts, (_, existing) =>
        {
            existing.Dispose();
            return cts;
        });

        return new Operation(this, key, cts);
    }

    /// <summary>Оборвать всё, что идёт с этим устройством. Возвращает, было ли что рвать.</summary>
    public bool CancelFor(DeviceId peer)
    {
        if (!_running.TryRemove(peer.ToString(), out CancellationTokenSource? cts))
            return false;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Операция уже завершилась сама — отменять нечего.
        }

        cts.Dispose();
        return true;
    }

    /// <summary>Сколько операций сейчас идёт.</summary>
    public int Count => _running.Count;

    public void Dispose()
    {
        foreach (CancellationTokenSource cts in _running.Values)
            cts.Dispose();
        _running.Clear();
    }

    /// <summary>Область идущей операции: снимает регистрацию по завершении.</summary>
    public sealed class Operation(PeerOperations owner, string key, CancellationTokenSource cts) : IDisposable
    {
        /// <summary>Токен, который отменится при отзыве доверия к устройству.</summary>
        public CancellationToken Token { get; } = cts.Token;

        public void Dispose()
        {
            if (owner._running.TryRemove(new KeyValuePair<string, CancellationTokenSource>(key, cts)))
                cts.Dispose();
        }
    }
}
