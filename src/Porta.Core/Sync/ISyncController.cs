using Porta.Core.Discovery;

namespace Porta.Core.Sync;

/// <summary>
/// Запуск синхронизации с устройством. Реализация (QUIC-транспорт) внедряется головой;
/// используется и кнопкой в UI, и авто-синхронизацией. См. docs/features/22-auto-sync.md.
/// </summary>
public interface ISyncController
{
    /// <summary>Синхронизировать хранилища с устройством. Возвращает краткий статус.</summary>
    Task<string> SyncWithPeerAsync(DiscoveredPeer peer, CancellationToken cancellationToken = default);
}
