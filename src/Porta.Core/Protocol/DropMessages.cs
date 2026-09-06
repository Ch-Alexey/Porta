using MessagePack;

namespace Porta.Core.Protocol;

/// <summary>Файл в предложении передачи (метаданные, без содержимого).</summary>
/// <param name="RelativePath">Путь внутри передачи ('/' как разделитель).</param>
/// <param name="Size">Размер в байтах.</param>
/// <param name="ModifiedAtUnixMs">Время изменения (Unix-мс, UTC).</param>
[MessagePackObject]
public sealed record DropFileInfo(
    [property: Key(0)] string RelativePath,
    [property: Key(1)] long Size,
    [property: Key(2)] long ModifiedAtUnixMs);

/// <summary>
/// Предложение разовой передачи: что отправитель хочет передать. Получатель решает,
/// принимать ли. См. docs/features/26-drop-transfer.md.
/// </summary>
[MessagePackObject]
public sealed record DropOfferMessage(
    [property: Key(0)] string TransferId,
    [property: Key(1)] IReadOnlyList<DropFileInfo> Files)
{
    /// <summary>Суммарный объём передачи.</summary>
    [IgnoreMember]
    public long TotalSize => Files.Sum(f => f.Size);
}

/// <summary>Решение получателя по предложению.</summary>
[MessagePackObject]
public sealed record DropDecisionMessage(
    [property: Key(0)] bool Accepted,
    [property: Key(1)] string? Reason = null);

/// <summary>Начало содержимого одного файла.</summary>
[MessagePackObject]
public sealed record DropFileHeaderMessage(
    [property: Key(0)] string RelativePath,
    [property: Key(1)] long Size,
    [property: Key(2)] long ModifiedAtUnixMs,
    [property: Key(3)] byte[] ContentHash);

/// <summary>
/// Кусок содержимого. <paramref name="IsLast"/> явно завершает файл — пустой последний
/// кусок корректен (пустой файл).
/// </summary>
[MessagePackObject]
public sealed record DropChunkMessage(
    [property: Key(0)] byte[] Data,
    [property: Key(1)] bool IsLast);

/// <summary>Подтверждение получателя по завершении всей передачи.</summary>
[MessagePackObject]
public sealed record DropCompleteMessage(
    [property: Key(0)] int FilesReceived);
