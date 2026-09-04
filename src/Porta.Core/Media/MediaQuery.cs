namespace Porta.Core.Media;

/// <summary>
/// Условия поиска медиа. Все поля необязательны — пустой запрос находит всё медиа.
/// См. docs/features/25-media-scan.md.
/// </summary>
public sealed class MediaQuery
{
    /// <summary>Какие типы искать; пусто — все.</summary>
    public IReadOnlyCollection<MediaKind> Kinds { get; init; } = [];

    /// <summary>Подстрока в имени файла (регистронезависимо); пусто — без фильтра.</summary>
    public string? NameContains { get; init; }

    /// <summary>Изменён не раньше этого момента.</summary>
    public DateTimeOffset? ModifiedFrom { get; init; }

    /// <summary>Изменён не позже этого момента.</summary>
    public DateTimeOffset? ModifiedTo { get; init; }

    /// <summary>Минимальный размер в байтах.</summary>
    public long MinSize { get; init; }

    /// <summary>Ограничение количества результатов; 0 — без ограничения.</summary>
    public int MaxResults { get; init; }

    /// <summary>Запрос без ограничений.</summary>
    public static MediaQuery All { get; } = new();

    /// <summary>Подходит ли файл под условия (кроме <see cref="MaxResults"/>).</summary>
    public bool Matches(MediaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (Kinds.Count > 0 && !Kinds.Contains(file.Kind))
            return false;
        if (!string.IsNullOrEmpty(NameContains)
            && !file.FileName.Contains(NameContains, StringComparison.OrdinalIgnoreCase))
            return false;
        if (ModifiedFrom is { } from && file.ModifiedAt < from)
            return false;
        if (ModifiedTo is { } to && file.ModifiedAt > to)
            return false;
        if (file.Size < MinSize)
            return false;

        return true;
    }
}
