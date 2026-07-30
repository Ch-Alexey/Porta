using System.Text;
using Porta.Core.Identity;

namespace Porta.Core.Tests.Identity;

public class DeviceIdentityTests
{
    [Fact]
    public void Generate_produces_id_and_public_key()
    {
        using var identity = DeviceIdentity.Generate();

        Assert.NotNull(identity.Id);
        Assert.NotEmpty(identity.ExportPublicKey());
    }

    [Fact]
    public void ImportPrivateKey_restores_same_id()
    {
        using var original = DeviceIdentity.Generate();

        using var restored = DeviceIdentity.ImportPrivateKey(original.ExportPrivateKey());

        Assert.Equal(original.Id, restored.Id);
    }

    [Fact]
    public void Sign_and_verify_roundtrip()
    {
        using var identity = DeviceIdentity.Generate();
        byte[] data = Encoding.UTF8.GetBytes("porta pairing challenge");

        byte[] signature = identity.Sign(data);

        Assert.True(identity.Verify(data, signature));
    }

    [Fact]
    public void Verify_fails_on_tampered_data()
    {
        using var identity = DeviceIdentity.Generate();
        byte[] signature = identity.Sign(Encoding.UTF8.GetBytes("original"));

        Assert.False(identity.Verify(Encoding.UTF8.GetBytes("tampered"), signature));
    }

    [Fact]
    public void VerifyWithPublicKey_accepts_own_key_and_rejects_foreign()
    {
        using var alice = DeviceIdentity.Generate();
        using var bob = DeviceIdentity.Generate();
        byte[] data = Encoding.UTF8.GetBytes("signed by alice");
        byte[] signature = alice.Sign(data);

        Assert.True(DeviceIdentity.VerifyWithPublicKey(alice.ExportPublicKey(), data, signature));
        Assert.False(DeviceIdentity.VerifyWithPublicKey(bob.ExportPublicKey(), data, signature));
    }
}
