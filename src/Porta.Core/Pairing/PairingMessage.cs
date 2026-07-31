using System.Buffers.Binary;
using System.Text;
using Porta.Core.Identity;

namespace Porta.Core.Pairing;

/// <summary>
/// Построение доменно-разделённого сообщения, которое подписывает joiner. Поля идут с
/// 4-байтовыми префиксами длины, чтобы исключить неоднозначность склейки.
/// См. docs/features/04-pairing.md.
/// </summary>
internal static class PairingMessage
{
    private const string Context = "porta.pairing.v1";

    public static byte[] Build(
        ReadOnlySpan<byte> secret,
        DeviceId inviterDeviceId,
        DeviceId joinerDeviceId,
        ReadOnlySpan<byte> joinerPublicKey)
    {
        byte[] context = Encoding.UTF8.GetBytes(Context);
        byte[] inviter = Encoding.UTF8.GetBytes(inviterDeviceId.ToString());
        byte[] joiner = Encoding.UTF8.GetBytes(joinerDeviceId.ToString());

        using var stream = new MemoryStream();
        WriteField(stream, context);
        WriteField(stream, secret);
        WriteField(stream, inviter);
        WriteField(stream, joiner);
        WriteField(stream, joinerPublicKey);
        return stream.ToArray();
    }

    private static void WriteField(Stream stream, ReadOnlySpan<byte> field)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)field.Length);
        stream.Write(length);
        stream.Write(field);
    }
}
