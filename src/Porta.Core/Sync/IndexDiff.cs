using Porta.Core.Indexing;

namespace Porta.Core.Sync;

/// <summary>Блок, которого не хватает локально и который надо запросить.</summary>
/// <param name="Hash">SHA-256 блока.</param>
/// <param name="Length">Длина блока (байты).</param>
public sealed record NeededBlock(byte[] Hash, int Length)
{
    public string HashHex => Convert.ToHexString(Hash);
}

/// <summary>
/// Результат сравнения индексов: что нужно принимающей стороне, чтобы догнать удалённую.
/// См. docs/features/08-index-exchange.md.
/// </summary>
/// <param name="FilesToUpdate">Новые или изменившиеся у удалённого файлы.</param>
/// <param name="MissingBlocks">Различные блоки, которых нет локально (с дедупликацией).</param>
public sealed record IndexDiff(
    IReadOnlyList<FileIndexEntry> FilesToUpdate,
    IReadOnlyList<NeededBlock> MissingBlocks);
