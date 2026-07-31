using MessagePack;

namespace Porta.Core.Indexing;

/// <summary>Блок файла: смещение, длина и SHA-256 содержимого блока.</summary>
/// <param name="Offset">Смещение блока от начала файла (байты).</param>
/// <param name="Length">Длина блока (байты).</param>
/// <param name="Hash">SHA-256 содержимого блока (32 байта).</param>
[MessagePackObject]
public sealed record ChunkInfo(
    [property: Key(0)] long Offset,
    [property: Key(1)] int Length,
    [property: Key(2)] byte[] Hash)
{
    /// <summary>Хеш блока в hex-виде — удобно для сравнения и логов.</summary>
    [IgnoreMember]
    public string HashHex => Convert.ToHexString(Hash);
}
