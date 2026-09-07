namespace Porta.Core.Sync;

/// <summary>
/// Ход передачи. Одна форма отчёта на отправку, приём и синхронизацию.
/// См. docs/features/33-progress-and-cancel.md.
/// </summary>
/// <param name="FilesDone">Сколько файлов завершено.</param>
/// <param name="FilesTotal">Сколько файлов всего (0 — неизвестно).</param>
/// <param name="BytesDone">Сколько байт передано.</param>
/// <param name="BytesTotal">Сколько байт всего (0 — неизвестно).</param>
/// <param name="CurrentFile">Файл, который идёт сейчас.</param>
public sealed record TransferProgress(
    int FilesDone,
    int FilesTotal,
    long BytesDone,
    long BytesTotal,
    string? CurrentFile = null)
{
    /// <summary>Доля выполненного 0..1; null, если общий объём неизвестен.</summary>
    public double? Fraction => BytesTotal > 0 ? Math.Clamp((double)BytesDone / BytesTotal, 0, 1) : null;
}

/// <summary>
/// Прореживает отчёты по времени: на быстром канале кусков сотни в секунду, и UI
/// захлебнётся перерисовкой. Финальный отчёт проходит всегда.
/// </summary>
public sealed class ThrottledProgress
{
    /// <summary>Минимальный интервал между отчётами.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    private readonly IProgress<TransferProgress>? _inner;
    private readonly TimeSpan _interval;
    private readonly TimeProvider _clock;
    private DateTimeOffset _last = DateTimeOffset.MinValue;

    public ThrottledProgress(
        IProgress<TransferProgress>? inner,
        TimeSpan? interval = null,
        TimeProvider? clock = null)
    {
        _inner = inner;
        _interval = interval ?? DefaultInterval;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Сообщить о ходе; отчёт может быть пропущен по времени.</summary>
    public void Report(TransferProgress progress)
    {
        if (_inner is null)
            return;

        DateTimeOffset now = _clock.GetUtcNow();
        if (now - _last < _interval)
            return;

        _last = now;
        _inner.Report(progress);
    }

    /// <summary>Сообщить обязательно (конец работы) — прореживание не применяется.</summary>
    public void ReportFinal(TransferProgress progress)
    {
        _last = _clock.GetUtcNow();
        _inner?.Report(progress);
    }
}
