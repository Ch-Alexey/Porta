using Porta.Core.Sync;

namespace Porta.Core.Tests.Sync;

/// <summary>Уборка брошенных времянок. См. docs/features/35-cleanup.md.</summary>
public class StaleFileCleanupTests : IDisposable
{
    private readonly string _root;

    public StaleFileCleanupTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    private string WriteFile(string name, TimeSpan age)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "содержимое");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    private string MakeDirectory(string name, TimeSpan age)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "блок.bin"), "данные");
        Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    [Fact]
    public void Old_drop_temporaries_are_removed()
    {
        string stale = WriteFile(".фото.jpg.porta-drop", TimeSpan.FromHours(3));

        Assert.Equal(1, StaleFileCleanup.CleanDropTemporaries(_root));
        Assert.False(File.Exists(stale));
    }

    [Fact]
    public void Fresh_drop_temporaries_are_left_alone()
    {
        // Могут принадлежать идущей передаче — в том числе второго экземпляра.
        string fresh = WriteFile(".фото.jpg.porta-drop", TimeSpan.FromMinutes(1));

        Assert.Equal(0, StaleFileCleanup.CleanDropTemporaries(_root));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void Real_files_are_never_touched()
    {
        string real = WriteFile("фото.jpg", TimeSpan.FromDays(30));

        StaleFileCleanup.CleanDropTemporaries(_root);

        Assert.True(File.Exists(real));
    }

    [Fact]
    public void Temporaries_in_subfolders_are_found()
    {
        string nested = WriteFile(Path.Combine("папка", ".док.txt.porta-drop"), TimeSpan.FromHours(3));

        Assert.Equal(1, StaleFileCleanup.CleanDropTemporaries(_root));
        Assert.False(File.Exists(nested));
    }

    [Fact]
    public void Old_block_spools_are_removed_and_fresh_ones_are_kept()
    {
        string stale = MakeDirectory("stale", TimeSpan.FromHours(5));
        string fresh = MakeDirectory("fresh", TimeSpan.FromMinutes(2));

        Assert.Equal(1, StaleFileCleanup.CleanBlockSpools(_root));
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(fresh));
    }

    [Fact]
    public void A_missing_folder_is_not_an_error()
    {
        string missing = Path.Combine(_root, "нет-такой");

        Assert.Equal(0, StaleFileCleanup.CleanDropTemporaries(missing));
        Assert.Equal(0, StaleFileCleanup.CleanBlockSpools(missing));
    }

    [Fact]
    public void An_empty_path_is_not_an_error()
        => Assert.Equal(0, StaleFileCleanup.CleanDropTemporaries("  "));

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
