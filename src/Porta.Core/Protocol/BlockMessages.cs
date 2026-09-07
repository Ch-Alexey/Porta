using MessagePack;

namespace Porta.Core.Protocol;

/// <summary>Запрос недостающих блоков по их хешам. См. docs/features/09-block-transfer.md.</summary>
[MessagePackObject]
public sealed record BlockRequestMessage(
    [property: Key(0)] IReadOnlyList<byte[]> Hashes);

/// <summary>Один блок: хеш и содержимое.</summary>
[MessagePackObject]
public sealed record BlockData(
    [property: Key(0)] byte[] Hash,
    [property: Key(1)] byte[] Data);

/// <summary>
/// Пачка блоков в потоковой отдаче. Пачки идут подряд, последняя помечена
/// <paramref name="IsLast"/> (может быть пустой). См. docs/features/27-block-streaming.md.
/// </summary>
[MessagePackObject]
public sealed record BlockBatchMessage(
    [property: Key(0)] IReadOnlyList<BlockData> Blocks,
    [property: Key(1)] bool IsLast);
