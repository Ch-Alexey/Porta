using System.Security.Cryptography.X509Certificates;
using Porta.Core.Identity;

namespace Porta.Core.Transport;

/// <summary>
/// Привязка X.509-сертификата к личности устройства (pinning). Доверяем не цепочке CA,
/// а совпадению публичного ключа сертификата с закреплённым Device ID.
/// См. docs/features/06-transport.md и ADR-0006.
/// </summary>
public static class DeviceCertificate
{
    /// <summary>Device ID, производный от публичного ключа сертификата.</summary>
    public static DeviceId FromCertificate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return DeviceId.FromPublicKey(certificate.PublicKey.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Совпадает ли предъявленный сертификат с ожидаемым (закреплённым) Device ID.
    /// Это обязательная проверка при QUIC-хендшейке.
    /// </summary>
    public static bool Matches(X509Certificate2 certificate, DeviceId expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return FromCertificate(certificate) == expected;
    }
}
