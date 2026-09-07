namespace Porta.Core.Sync;

/// <summary>Одна сохранённая версия файла.</summary>
/// <param name="Id">Идентификатор версии (имя файла в архиве).</param>
/// <param name="ArchivedAt">Когда версия была сохранена.</param>
/// <param name="Size">Размер в байтах.</param>
public sealed record ArchivedVersion(string Id, DateTimeOffset ArchivedAt, long Size);

/// <summary>
/// Архив прежних версий по всем хранилищам: посмотреть и откатиться.
/// См. docs/features/32-version-history.md.
/// </summary>
public interface IVersionArchive
{
    /// <summary>Хранилище версий для синхронизации конкретного хранилища.</summary>
    IVersionStore StoreFor(string storageId);

    /// <summary>Файлы хранилища, у которых есть сохранённые версии (пути через '/').</summary>
    IReadOnlyList<string> ListFiles(string storageId);

    /// <summary>Версии файла, от новых к старым.</summary>
    IReadOnlyList<ArchivedVersion> ListVersions(string storageId, string relativePath);

    /// <summary>
    /// Вернуть файлу содержимое выбранной версии. Текущее содержимое сначала
    /// архивируется — иначе откат сам станет способом потерять данные.
    /// </summary>
    void Restore(string storageId, string relativePath, string versionId, string storageRoot);
}

/// <summary>
/// Архив на файловой системе: подпапка на хранилище внутри общего корня (папка данных
/// приложения — намеренно вне папок хранилищ, чтобы копии не попадали в индекс).
/// </summary>
public sealed class FileSystemVersionArchive : IVersionArchive
{
    private readonly string _root;
    private readonly TimeProvider _clock;
    private readonly int _maxVersionsPerFile;

    public FileSystemVersionArchive(string root, TimeProvider? clock = null, int? maxVersionsPerFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = root;
        _clock = clock ?? TimeProvider.System;
        _maxVersionsPerFile = maxVersionsPerFile ?? FileSystemVersionStore.DefaultMaxVersionsPerFile;
    }

    public IVersionStore StoreFor(string storageId)
        => new FileSystemVersionStore(RootFor(storageId), _clock, _maxVersionsPerFile);

    public IReadOnlyList<string> ListFiles(string storageId)
    {
        string root = RootFor(storageId);
        if (!Directory.Exists(root))
            return [];

        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string archived in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (OriginalPath(root, archived) is { } original)
                files.Add(original);
        }

        return [.. files];
    }

    public IReadOnlyList<ArchivedVersion> ListVersions(string storageId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var store = new FileSystemVersionStore(RootFor(storageId), _clock, _maxVersionsPerFile);
        var result = new List<ArchivedVersion>();
        foreach (string path in store.ListVersions(relativePath))
        {
            var info = new FileInfo(path);
            result.Add(new ArchivedVersion(
                info.Name,
                ParseStamp(info.Name) ?? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                info.Length));
        }

        // От новых к старым: свежая версия — самая нужная.
        result.Reverse();
        return result;
    }

    public void Restore(string storageId, string relativePath, string versionId, string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);

        string archiveRoot = RootFor(storageId);
        // versionId приходит из UI — проверяем, что он не уводит за пределы архива.
        string versionPath = SafePath.Resolve(
            Path.GetDirectoryName(SafePath.Resolve(archiveRoot, relativePath))!, versionId);

        if (!File.Exists(versionPath))
            throw new FileNotFoundException($"Версия не найдена: {versionId}", versionPath);

        string target = SafePath.Resolve(storageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        // Сначала сохраняем то, что есть сейчас, — чтобы откат можно было откатить.
        StoreFor(storageId).Archive(target, relativePath);
        File.Copy(versionPath, target, overwrite: true);
    }

    private string RootFor(string storageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageId);
        // storageId — из БД, но путь всё равно строим через проверку.
        return SafePath.Resolve(_root, storageId);
    }

    /// <summary>Из имени копии («док~20260907-101530123.txt») вернуть исходный путь.</summary>
    private static string? OriginalPath(string root, string archivedPath)
    {
        string name = Path.GetFileName(archivedPath);
        int tilde = name.LastIndexOf('~');
        if (tilde <= 0)
            return null;

        string ext = Path.GetExtension(name);
        string original = name[..tilde] + ext;
        string dir = Path.GetRelativePath(root, Path.GetDirectoryName(archivedPath)!);
        string relative = dir == "." ? original : Path.Combine(dir, original);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static DateTimeOffset? ParseStamp(string archivedName)
    {
        int tilde = archivedName.LastIndexOf('~');
        if (tilde < 0)
            return null;

        string rest = archivedName[(tilde + 1)..];
        int dot = rest.LastIndexOf('.');
        string stamp = dot > 0 ? rest[..dot] : rest;

        return DateTime.TryParseExact(
            stamp, "yyyyMMdd-HHmmssfff", null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out DateTime parsed)
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : null;
    }
}
