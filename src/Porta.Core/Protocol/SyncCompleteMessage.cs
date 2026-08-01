using MessagePack;

namespace Porta.Core.Protocol;

/// <summary>
/// Финальное подтверждение приёма: принимающая сторона сообщает, что всё получено, чтобы
/// отдающая не закрыла соединение раньше времени. См. docs/features/17-versioned-sync.md.
/// </summary>
[MessagePackObject]
public sealed record SyncCompleteMessage(
    [property: Key(0)] bool Ok = true);
