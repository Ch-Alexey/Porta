namespace Porta.Core.Media;

/// <summary>
/// Классификация файлов по расширению — единственная точка правды о том, что считается
/// фото и видео. Регистронезависимо: это локальный поиск для человека, а не индекс,
/// который должен совпадать между устройствами. См. docs/features/25-media-scan.md.
/// </summary>
public static class MediaTypes
{
    private static readonly HashSet<string> Photos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif",
        ".heic", ".heif", ".avif",
        ".raw", ".dng", ".cr2", ".nef", ".arw", ".orf", ".rw2",
    };

    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".wmv", ".flv", ".3gp", ".mts", ".m2ts",
    };

    /// <summary>Определить тип по имени или пути файла; <c>null</c> — не медиа.</summary>
    public static MediaKind? Classify(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        string extension = Path.GetExtension(fileName);
        if (extension.Length == 0)
            return null;

        if (Photos.Contains(extension))
            return MediaKind.Photo;
        if (Videos.Contains(extension))
            return MediaKind.Video;
        return null;
    }
}
