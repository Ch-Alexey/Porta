using MessagePack;
using Porta.Core.Identity;
using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

public class VersionVectorTests
{
    private static readonly DeviceId X = DeviceIdentity.Generate().Id;
    private static readonly DeviceId Y = DeviceIdentity.Generate().Id;

    [Fact]
    public void Empty_vectors_are_identical()
    {
        Assert.Equal(VectorOrdering.Identical, VersionVector.Empty.Compare(VersionVector.Empty));
    }

    [Fact]
    public void Increment_raises_counter()
    {
        VersionVector v = VersionVector.Empty.Increment(X).Increment(X);
        Assert.Equal(2, v.Get(X));
        Assert.Equal(0, v.Get(Y));
    }

    [Fact]
    public void One_change_dominates_empty()
    {
        VersionVector a = VersionVector.Empty.Increment(X);

        Assert.Equal(VectorOrdering.Dominates, a.Compare(VersionVector.Empty));
        Assert.Equal(VectorOrdering.DominatedBy, VersionVector.Empty.Compare(a));
    }

    [Fact]
    public void Same_counters_are_identical()
    {
        VersionVector a = VersionVector.Empty.Increment(X);
        VersionVector b = VersionVector.Empty.Increment(X);

        Assert.Equal(VectorOrdering.Identical, a.Compare(b));
    }

    [Fact]
    public void Concurrent_edits_conflict()
    {
        VersionVector a = VersionVector.Empty.Increment(X);
        VersionVector b = VersionVector.Empty.Increment(Y);

        Assert.Equal(VectorOrdering.Conflict, a.Compare(b));
    }

    [Fact]
    public void Merge_is_elementwise_max_and_dominates_both()
    {
        VersionVector a = VersionVector.Empty.Increment(X).Increment(X);          // x:2
        VersionVector b = VersionVector.Empty.Increment(X).Increment(Y).Increment(Y).Increment(Y); // x:1, y:3

        VersionVector merged = a.Merge(b);

        Assert.Equal(2, merged.Get(X));
        Assert.Equal(3, merged.Get(Y));
        Assert.Equal(VectorOrdering.Dominates, merged.Compare(a));
        Assert.Equal(VectorOrdering.Dominates, merged.Compare(b));
    }

    [Fact]
    public void Roundtrips_through_messagepack()
    {
        VersionVector original = VersionVector.Empty.Increment(X).Increment(Y).Increment(Y);
        var options = MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

        byte[] bytes = MessagePackSerializer.Serialize(original, options);
        VersionVector restored = MessagePackSerializer.Deserialize<VersionVector>(bytes, options);

        Assert.Equal(VectorOrdering.Identical, original.Compare(restored));
        Assert.Equal(1, restored.Get(X));
        Assert.Equal(2, restored.Get(Y));
    }
}
