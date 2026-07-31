namespace Porta.Core.Sync;

/// <summary>
/// Хранилище предыдущих версий файлов. Сохраняет текущее содержимое перед перезаписью.
/// См. docs/features/11-versioning.md.
/// </summary>
public interface IVersionStore
{
    /// <summary>Заархивировать текущий файл как версию (no-op, если файла нет).</summary>
    /// <param name="filePath">Абсолютный путь к текущему файлу.</param>
    /// <param name="relativePath">Путь файла внутри хранилища (разделитель '/').</param>
    void Archive(string filePath, string relativePath);
}

/// <summary>
/// Файловое хранилище версий: копии с меткой времени в отдельном корне (вне папки
/// хранилища). См. docs/features/11-versioning.md.
/// </summary>
public sealed class FileSystemVersionStore : IVersionStore
{
    private readonly string _versionsRoot;
    private readonly TimeProvider _clock;

    public FileSystemVersionStore(string versionsRoot, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionsRoot);
        _versionsRoot = versionsRoot;
        _clock = clock ?? TimeProvider.System;
    }

    public void Archive(string filePath, string relativePath)
    {
        if (!File.Exists(filePath))
            return;

        (string destDir, string name, string ext) = Resolve(relativePath);
        Directory.CreateDirectory(destDir);

        string stamp = _clock.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmssfff");
        string dest = Path.Combine(destDir, $"{name}~{stamp}{ext}");
        File.Copy(filePath, dest, overwrite: false);
    }

    /// <summary>Список версий файла (по возрастанию имени = времени).</summary>
    public IReadOnlyList<string> ListVersions(string relativePath)
    {
        (string destDir, string name, string ext) = Resolve(relativePath);
        if (!Directory.Exists(destDir))
            return [];

        string[] matches = Directory.GetFiles(destDir, $"{name}~*{ext}");
        Array.Sort(matches, StringComparer.Ordinal);
        return matches;
    }

    private (string DestDir, string Name, string Ext) Resolve(string relativePath)
    {
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string dir = Path.GetDirectoryName(normalized) ?? string.Empty;
        return (
            Path.Combine(_versionsRoot, dir),
            Path.GetFileNameWithoutExtension(normalized),
            Path.GetExtension(normalized));
    }
}
