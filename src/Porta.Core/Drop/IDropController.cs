using Porta.Core.Discovery;

namespace Porta.Core.Drop;

/// <summary>
/// Разовая отправка файлов найденному устройству. Реализация (QUIC) внедряется головой,
/// поэтому view-модель тестируется без сети — по образцу
/// <see cref="Porta.Core.Sync.ISyncController"/>. См. docs/features/28-drop-ui.md.
/// </summary>
public interface IDropController
{
    Task<DropSendResult> SendAsync(
        DiscoveredPeer peer,
        IReadOnlyList<DropSourceFile> files,
        CancellationToken cancellationToken = default);
}
