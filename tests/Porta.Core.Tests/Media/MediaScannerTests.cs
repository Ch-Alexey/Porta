using Porta.Core.Indexing;
using Porta.Core.Media;

namespace Porta.Core.Tests.Media;

public class MediaScannerTests : IDisposable
{
    private readonly string _root;

    public MediaScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private string WriteFile(string relativePath, int size = 16)
    {
        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[size]);
        return full;
    }

    [Fact]
    public void Finds_media_in_nested_folders_and_skips_other_files()
    {
        WriteFile("a.jpg");
        WriteFile("sub/deep/b.mp4");
        WriteFile("sub/notes.txt");

        var names = new MediaScanner().Scan(_root).Select(m => m.FileName).Order().ToList();

        Assert.Equal(["a.jpg", "b.mp4"], names);
    }

    [Fact]
    public void Fills_metadata()
    {
        string path = WriteFile("holiday.jpg", size: 128);

        MediaFile file = Assert.Single(new MediaScanner().Scan(_root));

        Assert.Equal(path, file.FullPath);
        Assert.Equal("holiday.jpg", file.FileName);
        Assert.Equal(MediaKind.Photo, file.Kind);
        Assert.Equal(128, file.Size);
        Assert.True(file.ModifiedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Null(file.CapturedAt);
    }

    [Fact]
    public void Applies_query_filters()
    {
        WriteFile("a.jpg");
        WriteFile("b.mp4");

        var query = new MediaQuery { Kinds = [MediaKind.Video] };
        MediaFile file = Assert.Single(new MediaScanner().Scan(_root, query));

        Assert.Equal("b.mp4", file.FileName);
    }

    [Fact]
    public void MaxResults_stops_enumeration()
    {
        for (int i = 0; i < 10; i++)
            WriteFile($"sub{i}/photo{i}.jpg");

        var query = new MediaQuery { MaxResults = 3 };

        Assert.Equal(3, new MediaScanner().Scan(_root, query).Count());
    }

    [Fact]
    public void Skips_hidden_directories_by_default()
    {
        WriteFile(".cache/hidden.jpg");
        WriteFile("visible.jpg");

        MediaFile file = Assert.Single(new MediaScanner().Scan(_root));

        Assert.Equal("visible.jpg", file.FileName);
    }

    [Fact]
    public void Can_include_hidden_directories()
    {
        WriteFile(".cache/hidden.jpg");

        var scanner = new MediaScanner { SkipHiddenDirectories = false };

        Assert.Single(scanner.Scan(_root));
    }

    [Fact]
    public void Honours_ignore_rules()
    {
        WriteFile("node_modules/pkg/logo.png");
        WriteFile("photo.png");

        var ignore = new IgnoreRules(["node_modules"]);
        MediaFile file = Assert.Single(new MediaScanner().Scan(_root, ignore: ignore));

        Assert.Equal("photo.png", file.FileName);
    }

    [Fact]
    public void Enumeration_is_lazy_and_cancellable()
    {
        for (int i = 0; i < 5; i++)
            WriteFile($"photo{i}.jpg");

        using var cts = new CancellationTokenSource();
        IEnumerator<MediaFile> enumerator = new MediaScanner()
            .Scan(_root, cancellationToken: cts.Token)
            .GetEnumerator();

        Assert.True(enumerator.MoveNext());
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Missing_root_throws()
        => Assert.Throws<DirectoryNotFoundException>(
            () => new MediaScanner().Scan(Path.Combine(_root, "nope")).ToList());

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки — не повод валить тест.
        }
    }
}
