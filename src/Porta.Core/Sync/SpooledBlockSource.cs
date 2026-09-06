namespace Porta.Core.Sync;

/// <summary>
/// Принятые блоки на диске, а не в памяти: расход памяти не зависит от объёма синка.
/// Имя файла — hex хеша, поэтому блоки дедуплицируются сами.
/// См. docs/features/27-block-streaming.md.
/// </summary>
public sealed class SpooledBlockSource : IBlockSource, IDisposable
{
    private readonly string _directory;
    private bool _disposed;

    public SpooledBlockSource(string? directory = null)
    {
        _directory = directory ?? Path.Combine(Path.GetTempPath(), "porta-blocks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Сколько блоков сложено (после дедупликации).</summary>
    public int Count { get; private set; }

    /// <summary>Положить блок. Повторный блок с тем же хешем не пишется дважды.</summary>
    public void Add(byte[] hash, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(data);
        ObjectDisposedException.ThrowIf(_disposed, this);

        string path = PathFor(hash);
        if (File.Exists(path))
            return;

        File.WriteAllBytes(path, data);
        Count++;
    }

    public bool TryGet(byte[] hash, out byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string path = PathFor(hash);
        if (!File.Exists(path))
        {
            data = [];
            return false;
        }

        data = File.ReadAllBytes(path);
        return true;
    }

    private string PathFor(byte[] hash) => Path.Combine(_directory, Convert.ToHexString(hash));

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Времянка не удалилась — не повод ронять синк, который уже отработал.
        }
        catch (UnauthorizedAccessException)
        {
            // То же самое.
        }
    }
}
