using System.Text;
using Porta.Core.Common;

namespace Porta.Core.Tests.Common;

public class Base32Tests
{
    // Векторы из RFC 4648 (раздел 10), padding '=' убран — мы кодируем без него.
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Encode_matches_rfc4648_vectors(string input, string expected)
    {
        string actual = Base32.Encode(Encoding.ASCII.GetBytes(input));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Decode_is_inverse_of_encode()
    {
        var data = new byte[64];
        new Random(42).NextBytes(data);

        byte[] roundTripped = Base32.Decode(Base32.Encode(data));

        Assert.Equal(data, roundTripped);
    }

    [Fact]
    public void Decode_ignores_case()
    {
        Assert.Equal(Base32.Decode("MZXW6YTB"), Base32.Decode("mzxw6ytb"));
    }
}
