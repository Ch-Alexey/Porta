namespace Porta.Core.Pairing;

/// <summary>
/// Ответ присоединяющегося устройства (joiner) инициатору: его личность и подпись-
/// доказательство владения одноразовым кодом. См. docs/features/04-pairing.md.
/// </summary>
/// <param name="DeviceId">Device ID joiner'а (каноническая строка).</param>
/// <param name="PublicKey">Публичный ключ joiner'а (SPKI, DER).</param>
/// <param name="Name">Отображаемое имя устройства joiner'а.</param>
/// <param name="Signature">Подпись доменно-разделённого сообщения связывания.</param>
public sealed record PairingResponse(
    string DeviceId,
    byte[] PublicKey,
    string Name,
    byte[] Signature);
