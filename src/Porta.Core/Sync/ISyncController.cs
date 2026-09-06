using Porta.Core.Discovery;

namespace Porta.Core.Sync;

/// <summary>
/// Запуск синхронизации с устройством. Реализация (QUIC-транспорт) внедряется головой;
/// используется и кнопкой в UI, и авто-синхронизацией. См. docs/features/22-auto-sync.md.
/// </summary>
public interface ISyncController
{
    /// <summary>
    /// Синхронизировать хранилища с устройством. Возвращает краткий статус.
    /// </summary>
    /// <param name="peer">Найденное устройство.</param>
    /// <param name="trigger">
    /// Повод: по кнопке берутся все расшаренные хранилища, автоматически — только те,
    /// у кого включён авто-режим. См. docs/features/29-managing-what-exists.md.
    /// </param>
    /// <param name="cancellationToken">Отмена.</param>
    Task<string> SyncWithPeerAsync(
        DiscoveredPeer peer,
        SyncTrigger trigger = SyncTrigger.Manual,
        CancellationToken cancellationToken = default);
}
