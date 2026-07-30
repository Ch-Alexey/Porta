namespace Porta.Core.Indexing;

/// <summary>
/// Фиксированная таблица gear-хеша (256 × 64 бита), одинаковая на всех устройствах.
/// Генерируется детерминированным SplitMix64 (свой стабильный PRNG, не System.Random),
/// чтобы chunking совпадал между платформами и версиями .NET. См. docs/features/03-indexer.md.
/// </summary>
internal static class GearTable
{
    public static readonly ulong[] Values = Build();

    private static ulong[] Build()
    {
        var table = new ulong[256];
        for (ulong i = 0; i < 256; i++)
            table[i] = SplitMix64(i);
        return table;
    }

    private static ulong SplitMix64(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }
}
