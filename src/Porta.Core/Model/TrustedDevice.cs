using Porta.Core.Identity;

namespace Porta.Core.Model;

/// <summary>
/// Доверенное (удалённое) устройство: с ним разрешён обмен. Публичный ключ хранится
/// для проверки при соединении (pinning). См. docs/features/02-data-model.md.
/// </summary>
/// <param name="Id">Device ID.</param>
/// <param name="PublicKey">Публичный ключ (SubjectPublicKeyInfo, DER).</param>
/// <param name="Name">Отображаемое имя.</param>
/// <param name="AddedAt">Когда устройство добавлено в доверенные.</param>
/// <param name="LastSeenAt">Когда последний раз были на связи (или null).</param>
public sealed record TrustedDevice(
    DeviceId Id,
    byte[] PublicKey,
    string Name,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastSeenAt);
