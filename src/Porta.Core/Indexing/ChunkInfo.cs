namespace Porta.Core.Indexing;

/// <summary>Блок файла: смещение, длина и SHA-256 содержимого блока.</summary>
/// <param name="Offset">Смещение блока от начала файла (байты).</param>
/// <param name="Length">Длина блока (байты).</param>
/// <param name="Hash">SHA-256 содержимого блока (32 байта).</param>
public sealed record ChunkInfo(long Offset, int Length, byte[] Hash)
{
    /// <summary>Хеш блока в hex-виде — удобно для сравнения и логов.</summary>
    public string HashHex => Convert.ToHexString(Hash);
}
