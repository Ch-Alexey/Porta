namespace Porta.Core.Sync;

/// <summary>
/// Уборка времянок, брошенных при жёстком обрыве (процесс убит, питание отключено).
/// Обычные ошибки времянки убирают сами; это — про случай, когда убирать было некому.
/// См. docs/features/35-cleanup.md.
/// </summary>
public static class StaleFileCleanup
{
    /// <summary>
    /// Насколько старой должна быть времянка, чтобы считаться брошенной. С таймаутом
    /// чтения в 2 минуты часовой запас гарантирует, что мы не тронем чужую живую работу
    /// (например, времянки второго запущенного экземпляра).
    /// </summary>
    public static readonly TimeSpan DefaultMinAge = TimeSpan.FromHours(1);

    /// <summary>Удалить брошенные файлы разовых передач в папке загрузок.</summary>
    /// <returns>Сколько удалено.</returns>
    public static int CleanDropTemporaries(string downloadsFolder, TimeSpan? minAge = null, TimeProvider? clock = null)
    {
        if (string.IsNullOrWhiteSpace(downloadsFolder) || !Directory.Exists(downloadsFolder))
            return 0;

        DateTimeOffset cutoff = (clock ?? TimeProvider.System).GetUtcNow() - (minAge ?? DefaultMinAge);
        int removed = 0;

        foreach (string path in SafeEnumerateFiles(downloadsFolder))
        {
            if (!path.EndsWith(".porta-drop", StringComparison.Ordinal))
                continue;
            if (!IsOlderThan(path, cutoff))
                continue;

            if (TryDelete(() => File.Delete(path)))
                removed++;
        }

        return removed;
    }

    /// <summary>Удалить брошенные папки с блоками синка.</summary>
    /// <returns>Сколько удалено.</returns>
    public static int CleanBlockSpools(string? spoolRoot = null, TimeSpan? minAge = null, TimeProvider? clock = null)
    {
        spoolRoot ??= Path.Combine(Path.GetTempPath(), "porta-blocks");
        if (!Directory.Exists(spoolRoot))
            return 0;

        DateTimeOffset cutoff = (clock ?? TimeProvider.System).GetUtcNow() - (minAge ?? DefaultMinAge);
        int removed = 0;

        foreach (string dir in SafeEnumerateDirectories(spoolRoot))
        {
            if (!IsOlderThan(dir, cutoff, directory: true))
                continue;

            if (TryDelete(() => Directory.Delete(dir, recursive: true)))
                removed++;
        }

        return removed;
    }

    private static bool IsOlderThan(string path, DateTimeOffset cutoff, bool directory = false)
    {
        try
        {
            DateTime written = directory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
            return new DateTimeOffset(written, TimeSpan.Zero) < cutoff;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryDelete(Action delete)
    {
        try
        {
            delete();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Не смогли убрать — не повод не запускать приложение.
            return false;
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        try
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.GetDirectories(root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
