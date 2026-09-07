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
    /// <summary>
    /// Сколько версий одного файла хранить. Ограничение по числу, а не по объёму:
    /// «последние 10 правок» — гарантия, которую человек держит в голове.
    /// См. docs/features/32-version-history.md.
    /// </summary>
    public const int DefaultMaxVersionsPerFile = 10;

    private readonly string _versionsRoot;
    private readonly TimeProvider _clock;

    public FileSystemVersionStore(string versionsRoot, TimeProvider? clock = null, int? maxVersionsPerFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionsRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxVersionsPerFile ?? DefaultMaxVersionsPerFile, 1);
        _versionsRoot = versionsRoot;
        _clock = clock ?? TimeProvider.System;
        MaxVersionsPerFile = maxVersionsPerFile ?? DefaultMaxVersionsPerFile;
    }

    /// <summary>Сколько версий одного файла хранится.</summary>
    public int MaxVersionsPerFile { get; }

    public void Archive(string filePath, string relativePath)
    {
        if (!File.Exists(filePath))
            return;

        (string destDir, string name, string ext) = Resolve(relativePath);
        Directory.CreateDirectory(destDir);

        File.Copy(filePath, FreeStampedPath(destDir, name, ext), overwrite: false);
        Prune(relativePath);
    }

    /// <summary>
    /// Свободное имя с меткой времени. Две версии в одну миллисекунду — не выдумка:
    /// файл могут изменить и синхронизировать дважды подряд, а откат архивирует текущее
    /// содержимое сразу перед записью. Раньше это роняло синхронизацию с
    /// «file already exists». Сдвигаем метку на миллисекунду вперёд, пока имя не
    /// освободится: формат сохраняется, порядок по имени остаётся порядком по времени.
    /// </summary>
    private string FreeStampedPath(string destDir, string name, string ext)
    {
        DateTime stamp = _clock.GetUtcNow().UtcDateTime;
        while (true)
        {
            string candidate = Path.Combine(destDir, $"{name}~{stamp:yyyyMMdd-HHmmssfff}{ext}");
            if (!File.Exists(candidate))
                return candidate;
            stamp = stamp.AddMilliseconds(1);
        }
    }

    /// <summary>Список версий файла (по возрастанию имени = времени).</summary>
    public IReadOnlyList<string> ListVersions(string relativePath)
    {
        (string destDir, string name, string ext) = Resolve(relativePath);
        if (!Directory.Exists(destDir))
            return [];

        // Имена файлов сравниваем сами, а не шаблоном GetFiles: в имени хранилища могут
        // быть символы, которые шаблон трактует по-своему.
        string prefix = name + "~";
        var matches = Directory.GetFiles(destDir)
            .Where(f =>
            {
                string fileName = Path.GetFileName(f);
                return fileName.StartsWith(prefix, StringComparison.Ordinal)
                    && fileName.EndsWith(ext, StringComparison.Ordinal);
            })
            .ToArray();

        // Метка времени в имени монотонна, поэтому порядок строк = порядок по времени.
        Array.Sort(matches, StringComparer.Ordinal);
        return matches;
    }

    /// <summary>Удалить самые старые версии сверх лимита.</summary>
    private void Prune(string relativePath)
    {
        IReadOnlyList<string> versions = ListVersions(relativePath);
        for (int i = 0; i < versions.Count - MaxVersionsPerFile; i++)
        {
            try
            {
                File.Delete(versions[i]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Не удалось удалить старую копию — не повод ронять синхронизацию.
            }
        }
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
