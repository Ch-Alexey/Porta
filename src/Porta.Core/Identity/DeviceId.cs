using System.Security.Cryptography;
using System.Text;
using Porta.Core.Common;

namespace Porta.Core.Identity;

/// <summary>
/// Стабильный идентификатор устройства = Base32(SHA-256(публичный ключ в формате SPKI)).
/// Каноническая форма — 52 символа Base32 в верхнем регистре без разделителей.
/// См. docs/features/01-identity.md.
/// </summary>
public sealed class DeviceId : IEquatable<DeviceId>
{
    /// <summary>Длина канонической формы: SHA-256 (32 байта) в Base32 без padding.</summary>
    public const int CanonicalLength = 52;

    private const int DisplayGroupSize = 8;

    private readonly string _canonical;

    private DeviceId(string canonical) => _canonical = canonical;

    /// <summary>Вычислить Device ID из публичного ключа (SubjectPublicKeyInfo, DER).</summary>
    public static DeviceId FromPublicKey(ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(subjectPublicKeyInfo, hash);
        return new DeviceId(Base32.Encode(hash));
    }

    /// <summary>
    /// Разобрать Device ID из строки. Допускаются разделители (пробелы, дефисы) и любой
    /// регистр — они нормализуются.
    /// </summary>
    public static DeviceId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("Device ID пустой.");

        string canonical = Normalize(value);

        if (canonical.Length != CanonicalLength)
            throw new FormatException(
                $"Device ID должен содержать {CanonicalLength} символов Base32, получено {canonical.Length}.");

        foreach (char c in canonical)
            if (!IsBase32Char(c))
                throw new FormatException($"Недопустимый символ '{c}' в Device ID.");

        return new DeviceId(canonical);
    }

    public static bool TryParse(string? value, out DeviceId? deviceId)
    {
        try
        {
            deviceId = value is null ? null : Parse(value);
            return deviceId is not null;
        }
        catch (FormatException)
        {
            deviceId = null;
            return false;
        }
    }

    /// <summary>Каноническая форма (52 символа, без разделителей).</summary>
    public override string ToString() => _canonical;

    /// <summary>Форма для показа пользователю: группы по 8 символов через дефис.</summary>
    public string ToDisplayString()
    {
        var sb = new StringBuilder(_canonical.Length + _canonical.Length / DisplayGroupSize);
        for (int i = 0; i < _canonical.Length; i++)
        {
            if (i > 0 && i % DisplayGroupSize == 0)
                sb.Append('-');
            sb.Append(_canonical[i]);
        }
        return sb.ToString();
    }

    private static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (ch is '-' or ' ')
                continue;
            sb.Append(char.ToUpperInvariant(ch));
        }
        return sb.ToString();
    }

    private static bool IsBase32Char(char c) => c is (>= 'A' and <= 'Z') or (>= '2' and <= '7');

    public bool Equals(DeviceId? other) => other is not null && _canonical == other._canonical;

    public override bool Equals(object? obj) => obj is DeviceId other && Equals(other);

    public override int GetHashCode() => _canonical.GetHashCode(StringComparison.Ordinal);

    public static bool operator ==(DeviceId? left, DeviceId? right) => Equals(left, right);

    public static bool operator !=(DeviceId? left, DeviceId? right) => !Equals(left, right);
}
