using Porta.Core.Identity;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

/// <summary>Обрыв операций при отзыве доверия. См. docs/features/34-ui-polish.md.</summary>
public class PeerOperationsTests
{
    private static readonly DeviceId PeerA = DeviceIdentity.Generate().Id;
    private static readonly DeviceId PeerB = DeviceIdentity.Generate().Id;

    [Fact]
    public void Cancelling_a_peer_cancels_its_running_operation()
    {
        using var operations = new PeerOperations();
        using PeerOperations.Operation op = operations.Begin(PeerA);

        Assert.True(operations.CancelFor(PeerA));
        Assert.True(op.Token.IsCancellationRequested);
    }

    [Fact]
    public void Other_peers_are_not_touched()
    {
        using var operations = new PeerOperations();
        using PeerOperations.Operation a = operations.Begin(PeerA);
        using PeerOperations.Operation b = operations.Begin(PeerB);

        operations.CancelFor(PeerA);

        Assert.True(a.Token.IsCancellationRequested);
        Assert.False(b.Token.IsCancellationRequested);
    }

    [Fact]
    public void Cancelling_a_peer_with_nothing_running_is_reported_as_such()
    {
        using var operations = new PeerOperations();

        Assert.False(operations.CancelFor(PeerA));
    }

    [Fact]
    public void Finished_operations_stop_being_tracked()
    {
        using var operations = new PeerOperations();
        using (operations.Begin(PeerA))
            Assert.Equal(1, operations.Count);

        Assert.Equal(0, operations.Count);
        Assert.False(operations.CancelFor(PeerA));
    }

    [Fact]
    public void Callers_own_cancellation_still_works()
    {
        using var operations = new PeerOperations();
        using var cts = new CancellationTokenSource();
        using PeerOperations.Operation op = operations.Begin(PeerA, cts.Token);

        cts.Cancel();

        Assert.True(op.Token.IsCancellationRequested);
    }
}
