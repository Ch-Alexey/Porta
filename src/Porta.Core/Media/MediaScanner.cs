using Porta.Core.Indexing;

namespace Porta.Core.Media;

/// <summary>
/// Поиск фото и видео в дереве папок. Ленивое перечисление: вызывающий может
/// остановиться в любой момент, не дожидаясь конца обхода диска.
/// См. docs/features/25-media-scan.md.
/// </summary>
public sealed class MediaScanner
{
    /// <summary>Пропускать ли скрытые папки (имя с точки или атрибут Hidden).</summary>
    public bool SkipHiddenDirectories { get; init; } = true;

    /// <summary>
    /// Обойти дерево от <paramref name="rootPath"/> и вернуть подходящие медиафайлы.
    /// Недоступные подпапки пропускаются, символические ссылки не разворачиваются.
    /// </summary>
    /// <param name="rootPath">Корень обхода.</param>
    /// <param name="query">Условия поиска; <c>null</c> — всё медиа.</param>
    /// <param name="ignore">Дополнительные правила исключения (пути относительно корня).</param>
    /// <param name="cancellationToken">Отмена обхода.</param>
    public IEnumerable<MediaFile> Scan(
        string rootPath,
        MediaQuery? query = null,
        IgnoreRules? ignore = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Папка для поиска медиа не найдена: {rootPath}");

        query ??= MediaQuery.All;
        ignore ??= IgnoreRules.Empty;
        return Walk(Path.GetFullPath(rootPath), query, ignore, cancellationToken);
    }

    private IEnumerable<MediaFile> Walk(
        string root,
        MediaQuery query,
        IgnoreRules ignore,
        CancellationToken cancellationToken)
    {
        int found = 0;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();

            foreach (string file in SafeEnumerate(directory, directories: false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (MediaTypes.Classify(file) is not { } kind)
                    continue;
                if (ignore.IsIgnored(RelativePath(root, file)))
                    continue;
                if (Describe(file, kind) is not { } media || !query.Matches(media))
                    continue;

                yield return media;

                if (query.MaxResults > 0 && ++found >= query.MaxResults)
                    yield break;
            }

            foreach (string child in SafeEnumerate(directory, directories: true))
            {
                if (!ShouldEnter(child))
                    continue;
                if (ignore.IsIgnored(RelativePath(root, child)))
                    continue;
                pending.Push(child);
            }
        }
    }

    private bool ShouldEnter(string directory)
    {
        try
        {
            var info = new DirectoryInfo(directory);

            // Символические ссылки не разворачиваем — защита от циклов.
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;

            if (!SkipHiddenDirectories)
                return true;

            return !info.Name.StartsWith('.') && !info.Attributes.HasFlag(FileAttributes.Hidden);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static MediaFile? Describe(string path, MediaKind kind)
    {
        try
        {
            var info = new FileInfo(path);
            return new MediaFile(
                info.FullName,
                info.Name,
                kind,
                info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Файл исчез или недоступен между перечислением и чтением метаданных.
            return null;
        }
    }

    /// <summary>
    /// Перечислить содержимое папки, проглатывая недоступность: на реальном устройстве
    /// в домашней папке всегда найдётся папка без прав, и падать из-за неё нельзя.
    /// </summary>
    private static IReadOnlyList<string> SafeEnumerate(string directory, bool directories)
    {
        try
        {
            return directories
                ? Directory.GetDirectories(directory)
                : Directory.GetFiles(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string RelativePath(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
