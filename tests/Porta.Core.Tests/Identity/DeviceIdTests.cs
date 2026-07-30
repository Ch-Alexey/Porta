using System.Security.Cryptography;
using Porta.Core.Identity;

namespace Porta.Core.Tests.Identity;

public class DeviceIdTests
{
    private static byte[] SamplePublicKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportSubjectPublicKeyInfo();
    }

    [Fact]
    public void FromPublicKey_is_deterministic()
    {
        byte[] spki = SamplePublicKey();

        DeviceId a = DeviceId.FromPublicKey(spki);
        DeviceId b = DeviceId.FromPublicKey(spki);

        Assert.Equal(a, b);
        Assert.Equal(a.ToString(), b.ToString());
    }

    [Fact]
    public void Different_keys_give_different_ids()
    {
        DeviceId a = DeviceId.FromPublicKey(SamplePublicKey());
        DeviceId b = DeviceId.FromPublicKey(SamplePublicKey());

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Canonical_form_has_expected_length_and_alphabet()
    {
        string canonical = DeviceId.FromPublicKey(SamplePublicKey()).ToString();

        Assert.Equal(DeviceId.CanonicalLength, canonical.Length);
        Assert.All(canonical, c => Assert.True(c is (>= 'A' and <= 'Z') or (>= '2' and <= '7')));
    }

    [Fact]
    public void Parse_roundtrips_canonical_form()
    {
        DeviceId original = DeviceId.FromPublicKey(SamplePublicKey());

        DeviceId parsed = DeviceId.Parse(original.ToString());

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Parse_normalizes_separators_and_case()
    {
        DeviceId original = DeviceId.FromPublicKey(SamplePublicKey());

        DeviceId fromDisplay = DeviceId.Parse(original.ToDisplayString().ToLowerInvariant());

        Assert.Equal(original, fromDisplay);
    }

    [Fact]
    public void ToDisplayString_groups_by_eight()
    {
        string display = DeviceId.FromPublicKey(SamplePublicKey()).ToDisplayString();

        string[] groups = display.Split('-');
        Assert.All(groups[..^1], g => Assert.Equal(8, g.Length));
        Assert.Equal(DeviceId.CanonicalLength, display.Replace("-", "").Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOOSHORT")]
    [InlineData("MZXW6YTBOI0189MZXW6YTBOI0189MZXW6YTBOI0189MZXW6YTB01")] // содержит '0','1','8','9'
    public void Parse_rejects_invalid_input(string value)
    {
        Assert.Throws<FormatException>(() => DeviceId.Parse(value));
    }

    [Fact]
    public void TryParse_returns_false_for_invalid()
    {
        Assert.False(DeviceId.TryParse("nope", out DeviceId? id));
        Assert.Null(id);
    }
}
