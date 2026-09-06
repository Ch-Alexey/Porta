using MessagePack;

namespace Porta.Core.Protocol;

/// <summary>Чего инициатор хочет от сессии. См. docs/features/26-drop-transfer.md.</summary>
public enum SessionIntentKind
{
    /// <summary>Синхронизация хранилища.</summary>
    Sync = 0,

    /// <summary>Разовая передача файлов.</summary>
    Drop = 1,
}

/// <summary>
/// Второе сообщение сессии (сразу после рукопожатия): инициатор объявляет намерение,
/// принимающая сторона по нему выбирает диалог.
/// </summary>
[MessagePackObject]
public sealed record SessionIntentMessage(
    [property: Key(0)] SessionIntentKind Kind);
