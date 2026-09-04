using Porta.Core.Media;

namespace Porta.Core.Tests.Media;

public class MediaQueryTests
{
    private static MediaFile File(
        string name = "photo.jpg",
        MediaKind kind = MediaKind.Photo,
        long size = 1024,
        int daysAgo = 0)
        => new(
            "/root/" + name,
            name,
            kind,
            size,
            DateTimeOffset.UtcNow.AddDays(-daysAgo));

    [Fact]
    public void Empty_query_matches_everything()
    {
        Assert.True(MediaQuery.All.Matches(File()));
        Assert.True(MediaQuery.All.Matches(File("clip.mp4", MediaKind.Video)));
    }

    [Fact]
    public void Filters_by_kind()
    {
        var query = new MediaQuery { Kinds = [MediaKind.Video] };

        Assert.False(query.Matches(File()));
        Assert.True(query.Matches(File("clip.mp4", MediaKind.Video)));
    }

    [Fact]
    public void Filters_by_name_case_insensitively()
    {
        var query = new MediaQuery { NameContains = "OTPUSK" };

        Assert.True(query.Matches(File("otpusk-2024.jpg")));
        Assert.False(query.Matches(File("work.jpg")));
    }

    [Fact]
    public void Filters_by_modification_range()
    {
        var query = new MediaQuery { ModifiedFrom = DateTimeOffset.UtcNow.AddDays(-5) };

        Assert.True(query.Matches(File(daysAgo: 1)));
        Assert.False(query.Matches(File(daysAgo: 10)));
    }

    [Fact]
    public void Filters_by_min_size()
    {
        var query = new MediaQuery { MinSize = 2048 };

        Assert.False(query.Matches(File(size: 1024)));
        Assert.True(query.Matches(File(size: 4096)));
    }
}
