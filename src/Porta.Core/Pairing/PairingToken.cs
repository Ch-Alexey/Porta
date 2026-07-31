using MessagePack;
using Porta.Core.Identity;

namespace Porta.Core.Pairing;

/// <summary>
/// Данные, зашитые в QR/ссылку для связывания. Несёт публичный ключ инициатора (его
/// закрепляет joiner), адреса, одноразовый код и срок годности.
/// См. docs/features/04-pairing.md.
/// </summary>
[MessagePackObject]
public sealed record PairingToken(
    [property: Key(0)] int Version,
    [property: Key(1)] byte[] InviterPublicKey,
    [property: Key(2)] string[] Addresses,
    [property: Key(3)] byte[] Secret,
    [property: Key(4)] long ExpiresAtUnix)
{
    /// <summary>Текущая версия формата токена.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Device ID инициатора — производный от <see cref="InviterPublicKey"/>.</summary>
    [IgnoreMember]
    public DeviceId InviterDeviceId => DeviceId.FromPublicKey(InviterPublicKey);
}
