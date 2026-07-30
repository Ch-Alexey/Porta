namespace Porta.Core.Indexing;

/// <summary>
/// Параметры content-defined chunking. ВНИМАНИЕ: должны быть одинаковыми на всех
/// устройствах, иначе блоки не совпадут. См. docs/features/03-indexer.md.
/// </summary>
public sealed class ChunkerOptions
{
    /// <summary>Минимальный размер блока — раньше не режем.</summary>
    public required int MinSize { get; init; }

    /// <summary>Целевой средний размер блока — точка переключения масок.</summary>
    public required int AvgSize { get; init; }

    /// <summary>Максимальный размер блока — режем принудительно.</summary>
    public required int MaxSize { get; init; }

    /// <summary>Строгая маска (до среднего размера): больше бит → резать труднее.</summary>
    public required ulong MaskS { get; init; }

    /// <summary>Мягкая маска (после среднего размера): меньше бит → резать легче.</summary>
    public required ulong MaskL { get; init; }

    /// <summary>Параметры по умолчанию: 2 KiB / 8 KiB / 64 KiB, нормализация ±2 бита.</summary>
    public static ChunkerOptions Default { get; } = new()
    {
        MinSize = 2 * 1024,
        AvgSize = 8 * 1024,   // 2^13 → базовые 13 бит
        MaxSize = 64 * 1024,
        MaskS = TopBits(15),  // 13 + 2
        MaskL = TopBits(11),  // 13 - 2
    };

    /// <summary>Маска из <paramref name="count"/> старших бит 64-битного слова.</summary>
    internal static ulong TopBits(int count)
        => count <= 0 ? 0UL : ulong.MaxValue << (64 - count);
}
