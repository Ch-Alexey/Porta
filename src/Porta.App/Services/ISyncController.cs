using System.Threading;
using System.Threading.Tasks;
using Porta.Core.Discovery;

namespace Porta.App.Services;

/// <summary>
/// Запуск синхронизации с найденным устройством. Реализация (QUIC-транспорт) внедряется
/// головой; VM зависит только от этой абстракции. См. docs/features/21-sync-from-ui.md.
/// </summary>
public interface ISyncController
{
    /// <summary>Синхронизировать хранилища с устройством. Возвращает краткий статус.</summary>
    Task<string> SyncWithPeerAsync(DiscoveredPeer peer, CancellationToken cancellationToken = default);
}
