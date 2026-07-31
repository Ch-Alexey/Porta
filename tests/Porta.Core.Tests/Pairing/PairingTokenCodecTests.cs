using Porta.Core.Identity;
using Porta.Core.Pairing;

namespace Porta.Core.Tests.Pairing;

public class PairingTokenCodecTests
{
    private static PairingToken SampleToken()
    {
        using var identity = DeviceIdentity.Generate();
        return new PairingToken(
            PairingToken.CurrentVersion,
            identity.ExportPublicKey(),
            ["192.168.1.10:12345", "10.0.0.5:12345"],
            [1, 2, 3, 4, 5],
            ExpiresAtUnix: 1_700_000_300);
    }

    [Fact]
    public void Encode_then_Decode_roundtrips()
    {
        PairingToken token = SampleToken();

        PairingToken decoded = PairingTokenCodec.Decode(PairingTokenCodec.Encode(token));

        // record с полями byte[] сравнивается по ссылке, поэтому сверяем поля явно.
        Assert.Equal(token.Version, decoded.Version);
        Assert.Equal(token.InviterPublicKey, decoded.InviterPublicKey);
        Assert.Equal(token.Addresses, decoded.Addresses);
        Assert.Equal(token.Secret, decoded.Secret);
        Assert.Equal(token.ExpiresAtUnix, decoded.ExpiresAtUnix);
        Assert.Equal(token.InviterDeviceId, decoded.InviterDeviceId);
    }

    [Fact]
    public void Encoded_string_has_porta_prefix()
    {
        Assert.StartsWith("porta:", PairingTokenCodec.Encode(SampleToken()));
    }

    [Fact]
    public void InviterDeviceId_is_derived_from_public_key()
    {
        using var identity = DeviceIdentity.Generate();
        var token = SampleToken() with { InviterPublicKey = identity.ExportPublicKey() };

        Assert.Equal(identity.Id, token.InviterDeviceId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("porta:!!!not-base64!!!")]
    public void Decode_rejects_invalid_input(string value)
    {
        Assert.Throws<FormatException>(() => PairingTokenCodec.Decode(value));
    }
}
