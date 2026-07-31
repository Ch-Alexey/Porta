using Porta.Core.Identity;
using Porta.Core.Transport;

namespace Porta.Core.Tests.Transport;

public class DeviceCertificateTests
{
    private static readonly DateTimeOffset NotBefore = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private static readonly DateTimeOffset NotAfter = NotBefore.AddYears(1);

    [Fact]
    public void Generated_certificate_has_private_key_and_device_id_in_subject()
    {
        using var identity = DeviceIdentity.Generate();

        using var cert = identity.CreateSelfSignedCertificate(NotBefore, NotAfter);

        Assert.True(cert.HasPrivateKey);
        Assert.Contains(identity.Id.ToString(), cert.Subject);
    }

    [Fact]
    public void Certificate_public_key_equals_device_public_key()
    {
        using var identity = DeviceIdentity.Generate();

        using var cert = identity.CreateSelfSignedCertificate(NotBefore, NotAfter);

        Assert.Equal(identity.ExportPublicKey(), cert.PublicKey.ExportSubjectPublicKeyInfo());
    }

    [Fact]
    public void Certificate_is_bound_to_device_id()
    {
        using var identity = DeviceIdentity.Generate();

        using var cert = identity.CreateSelfSignedCertificate(NotBefore, NotAfter);

        Assert.Equal(identity.Id, DeviceCertificate.FromCertificate(cert));
    }

    [Fact]
    public void Matches_accepts_own_id_and_rejects_foreign()
    {
        using var identity = DeviceIdentity.Generate();
        using var other = DeviceIdentity.Generate();
        using var cert = identity.CreateSelfSignedCertificate(NotBefore, NotAfter);

        Assert.True(DeviceCertificate.Matches(cert, identity.Id));
        Assert.False(DeviceCertificate.Matches(cert, other.Id));
    }

    [Fact]
    public void Validity_period_is_set()
    {
        using var identity = DeviceIdentity.Generate();

        using var cert = identity.CreateSelfSignedCertificate(NotBefore, NotAfter);

        // Сертификат хранит время в локальной зоне — сравниваем моменты в UTC.
        Assert.Equal(NotBefore.UtcDateTime, cert.NotBefore.ToUniversalTime());
        Assert.Equal(NotAfter.UtcDateTime, cert.NotAfter.ToUniversalTime());
    }
}
