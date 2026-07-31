using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Porta.Core.Identity;

/// <summary>
/// Криптографическая личность устройства: пара ключей ECDSA P-256 и производный
/// <see cref="DeviceId"/>. На ней держатся доверие (pinning публичного ключа) и
/// будущий TLS-сертификат для QUIC. См. docs/features/01-identity.md и ADR-0006.
/// </summary>
public sealed class DeviceIdentity : IDisposable
{
    private readonly ECDsa _key;

    private DeviceIdentity(ECDsa key)
    {
        _key = key;
        Id = DeviceId.FromPublicKey(key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>Идентификатор устройства, производный от публичного ключа.</summary>
    public DeviceId Id { get; }

    /// <summary>Сгенерировать новую личность (новая пара ключей P-256).</summary>
    public static DeviceIdentity Generate() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    /// <summary>Восстановить личность из приватного ключа в формате PKCS#8 (DER).</summary>
    public static DeviceIdentity ImportPrivateKey(ReadOnlySpan<byte> pkcs8PrivateKey)
    {
        var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
        return new DeviceIdentity(key);
    }

    /// <summary>Публичный ключ в формате SubjectPublicKeyInfo (DER) — им делятся при связывании.</summary>
    public byte[] ExportPublicKey() => _key.ExportSubjectPublicKeyInfo();

    /// <summary>
    /// Построить самоподписанный X.509-сертификат из ключа устройства для TLS/QUIC.
    /// Публичный ключ сертификата совпадает с ключом устройства, поэтому он привязан к
    /// <see cref="Id"/>. См. docs/features/06-transport.md.
    /// </summary>
    public X509Certificate2 CreateSelfSignedCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var request = new CertificateRequest($"CN={Id}", _key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>Приватный ключ в формате PKCS#8 (DER) — только для персистентности личности.</summary>
    internal byte[] ExportPrivateKey() => _key.ExportPkcs8PrivateKey();

    /// <summary>Подписать данные приватным ключом этого устройства (ECDSA/SHA-256).</summary>
    public byte[] Sign(ReadOnlySpan<byte> data) => _key.SignData(data, HashAlgorithmName.SHA256);

    /// <summary>Проверить подпись публичным ключом этого устройства.</summary>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        => _key.VerifyData(data, signature, HashAlgorithmName.SHA256);

    /// <summary>
    /// Проверить подпись чужим публичным ключом (SubjectPublicKeyInfo, DER) — так одно
    /// устройство проверяет данные, подписанные другим доверенным устройством.
    /// </summary>
    public static bool VerifyWithPublicKey(
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature)
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
        return key.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }

    public void Dispose() => _key.Dispose();
}
