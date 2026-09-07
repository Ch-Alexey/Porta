using Porta.Core.Identity;

namespace Porta.Core.Sync;

/// <summary>Что делать с файлом при синхронизации.</summary>
public enum SyncAction
{
    /// <summary>Принять удалённую версию.</summary>
    Accept,

    /// <summary>Ничего не делать (наша версия актуальна).</summary>
    Skip,

    /// <summary>Конфликт — сохранить обе версии.</summary>
    Conflict,
}

/// <summary>
/// Решение по файлу на основе version vectors и генерация имени конфликтной копии.
/// Чистая логика. См. docs/features/15-conflict-resolution.md.
/// </summary>
public static class ConflictResolver
{
    public static SyncAction Decide(VersionedFileEntry? local, VersionedFileEntry remote)
    {
        ArgumentNullException.ThrowIfNull(remote);

        if (local is null)
            return SyncAction.Accept;

        // Одинаковое содержимое — не конфликт, разрешать нечего. Проверяем это ДО разбора
        // порядка векторов: у двух устройств, никогда не синхронизировавшихся, векторы
        // параллельны, и раньше это плодило копию-двойник для каждого файла.
        // См. docs/features/31-audit-fixes.md.
        if (SameContent(local, remote))
            return SyncAction.Skip;

        return remote.Version.Compare(local.Version) switch
        {
            VectorOrdering.Dominates => SyncAction.Accept,
            VectorOrdering.DominatedBy => SyncAction.Skip,
            _ => SyncAction.Conflict,
        };
    }

    /// <summary>
    /// Имя конфликтной копии: маркер и метка времени вставляются перед расширением,
    /// каталог сохраняется. Разделитель пути — '/'.
    /// </summary>
    public static string ConflictName(string relativePath, DeviceId remoteDevice, DateTimeOffset when)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string ext = GetExtension(relativePath);
        string withoutExt = relativePath[..^ext.Length];
        string stamp = when.UtcDateTime.ToString("yyyyMMdd-HHmmss");
        string shortId = remoteDevice.ToString()[..7];
        return $"{withoutExt}.sync-conflict-{stamp}-{shortId}{ext}";
    }

    private static bool SameContent(VersionedFileEntry a, VersionedFileEntry b)
        => a.Entry.ContentHash.AsSpan().SequenceEqual(b.Entry.ContentHash);

    private static string GetExtension(string path)
    {
        int lastSlash = path.LastIndexOf('/');
        string fileName = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        int dot = fileName.LastIndexOf('.');
        // Точка в начале имени (напр. ".gitignore") — не расширение.
        return dot > 0 ? fileName[dot..] : string.Empty;
    }
}
