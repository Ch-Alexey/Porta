namespace Porta.Core.Media;

/// <summary>
/// Найденный медиафайл. Отличается от <see cref="Porta.Core.Indexing.FileIndexEntry"/>:
/// здесь быстрый обзор без чтения содержимого, без блоков и хэшей.
/// См. docs/features/25-media-scan.md.
/// </summary>
/// <param name="FullPath">Полный путь к файлу на этом устройстве.</param>
/// <param name="FileName">Имя файла.</param>
/// <param name="Kind">Фото или видео.</param>
/// <param name="Size">Размер в байтах.</param>
/// <param name="ModifiedAt">Время последнего изменения (UTC).</param>
/// <param name="CapturedAt">
/// Дата съёмки из EXIF — задел под следующий срез, сейчас всегда <c>null</c>.
/// </param>
public sealed record MediaFile(
    string FullPath,
    string FileName,
    MediaKind Kind,
    long Size,
    DateTimeOffset ModifiedAt,
    DateTimeOffset? CapturedAt = null);
