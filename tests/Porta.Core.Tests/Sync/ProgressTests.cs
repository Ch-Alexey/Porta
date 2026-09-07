using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

/// <summary>Прореживание отчётов. См. docs/features/33-progress-and-cancel.md.</summary>
public class ProgressTests
{
    private sealed class Collector : IProgress<TransferProgress>
    {
        public List<TransferProgress> Reports { get; } = [];

        public void Report(TransferProgress value) => Reports.Add(value);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    [Fact]
    public void Reports_closer_than_the_interval_are_dropped()
    {
        var sink = new Collector();
        var clock = new ManualClock();
        var throttled = new ThrottledProgress(sink, TimeSpan.FromMilliseconds(100), clock);

        for (int i = 0; i < 50; i++)
            throttled.Report(new TransferProgress(0, 1, i, 50));

        Assert.Single(sink.Reports);
    }

    [Fact]
    public void Reports_spaced_out_in_time_all_get_through()
    {
        var sink = new Collector();
        var clock = new ManualClock();
        var throttled = new ThrottledProgress(sink, TimeSpan.FromMilliseconds(100), clock);

        for (int i = 0; i < 5; i++)
        {
            throttled.Report(new TransferProgress(0, 1, i, 5));
            clock.Advance(TimeSpan.FromMilliseconds(150));
        }

        Assert.Equal(5, sink.Reports.Count);
    }

    [Fact]
    public void The_final_report_always_gets_through()
    {
        var sink = new Collector();
        var throttled = new ThrottledProgress(sink, TimeSpan.FromMinutes(1), new ManualClock());

        throttled.Report(new TransferProgress(0, 1, 1, 10));
        throttled.ReportFinal(new TransferProgress(1, 1, 10, 10));

        Assert.Equal(10, sink.Reports[^1].BytesDone);
    }

    [Fact]
    public void No_sink_means_no_work_and_no_crash()
    {
        var throttled = new ThrottledProgress(null);

        throttled.Report(new TransferProgress(0, 1, 1, 10));
        throttled.ReportFinal(new TransferProgress(1, 1, 10, 10));
    }

    [Theory]
    [InlineData(0, 100, 0.0)]
    [InlineData(50, 100, 0.5)]
    [InlineData(100, 100, 1.0)]
    [InlineData(150, 100, 1.0)]
    public void Fraction_is_clamped(long done, long total, double expected)
        => Assert.Equal(expected, new TransferProgress(0, 1, done, total).Fraction);

    [Fact]
    public void Unknown_total_gives_no_fraction()
        => Assert.Null(new TransferProgress(0, 0, 42, 0).Fraction);
}
