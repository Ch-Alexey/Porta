using MessagePack;

namespace Porta.Core.Protocol;

/// <summary>Версия прикладного протокола Porta.</summary>
public static class ProtocolVersion
{
    public const int Current = 1;
}

/// <summary>
/// Первое сообщение при установлении сессии: версия протокола и имя устройства.
/// См. docs/features/07-protocol.md.
/// </summary>
[MessagePackObject]
public sealed record HelloMessage(
    [property: Key(0)] int ProtocolVersion,
    [property: Key(1)] string DeviceName);
