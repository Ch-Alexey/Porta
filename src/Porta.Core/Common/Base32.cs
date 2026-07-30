using System.Text;

namespace Porta.Core.Common;

/// <summary>
/// Base32 (RFC 4648) без padding, верхний регистр. Используется для человекочитаемых
/// идентификаторов (напр. <see cref="Identity.DeviceId"/>).
/// </summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return string.Empty;

        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
            sb.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);

        return sb.ToString();
    }

    public static byte[] Decode(string value)
    {
        var bytes = new List<byte>(value.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in value)
        {
            int index = Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (index < 0)
                throw new FormatException($"Недопустимый символ Base32: '{c}'.");

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
