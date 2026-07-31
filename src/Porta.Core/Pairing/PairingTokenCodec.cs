using MessagePack;

namespace Porta.Core.Pairing;

/// <summary>
/// Кодирование токена связывания в строку для QR/ссылки и обратно.
/// Формат: <c>porta:</c> + Base64Url(MessagePack(token)). См. docs/features/04-pairing.md.
/// </summary>
public static class PairingTokenCodec
{
    private const string Prefix = "porta:";

    // Данные приходят из внешнего мира (сканирование QR) — помечаем как недоверенные.
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    public static string Encode(PairingToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        byte[] bytes = MessagePackSerializer.Serialize(token, Options);
        return Prefix + ToBase64Url(bytes);
    }

    public static PairingToken Decode(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new FormatException("Пустой токен связывания.");
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
            throw new FormatException($"Токен связывания должен начинаться с '{Prefix}'.");

        byte[] bytes;
        try
        {
            bytes = FromBase64Url(value[Prefix.Length..]);
            return MessagePackSerializer.Deserialize<PairingToken>(bytes, Options);
        }
        catch (Exception ex) when (ex is FormatException or MessagePackSerializationException)
        {
            throw new FormatException("Некорректный токен связывания.", ex);
        }
    }

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => "",
            _ => throw new FormatException("Некорректная длина Base64Url."),
        };
        return Convert.FromBase64String(padded);
    }
}
