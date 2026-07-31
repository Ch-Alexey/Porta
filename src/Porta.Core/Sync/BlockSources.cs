namespace Porta.Core.Sync;

/// <summary>Источник блоков по их хешу. См. docs/features/09-block-transfer.md.</summary>
public interface IBlockSource
{
    bool TryGet(byte[] hash, out byte[] data);
}

/// <summary>Блоки в памяти (напр. полученные от второй стороны).</summary>
public sealed class MemoryBlockSource : IBlockSource
{
    private readonly Dictionary<string, byte[]> _blocks = new(StringComparer.Ordinal);

    public MemoryBlockSource(IEnumerable<KeyValuePair<byte[], byte[]>> blocks)
    {
        foreach (var (hash, data) in blocks)
            _blocks[Convert.ToHexString(hash)] = data;
    }

    public bool TryGet(byte[] hash, out byte[] data)
        => _blocks.TryGetValue(Convert.ToHexString(hash), out data!);
}

/// <summary>Пробует источники по очереди — первый, где блок найден.</summary>
public sealed class CompositeBlockSource(params IBlockSource[] sources) : IBlockSource
{
    public bool TryGet(byte[] hash, out byte[] data)
    {
        foreach (IBlockSource source in sources)
            if (source.TryGet(hash, out data!))
                return true;
        data = [];
        return false;
    }
}
