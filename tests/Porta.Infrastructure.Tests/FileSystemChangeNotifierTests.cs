using Porta.Infrastructure.Sync;

namespace Porta.Infrastructure.Tests;

/// <summary>
/// Наблюдатель ФС поднимает дебаунснутый сигнал при изменении в папке.
/// Интеграционный (реальный FileSystemWatcher). См. docs/features/23-file-change-sync.md.
/// </summary>
[Trait("Category", "Integration")]
public class FileSystemChangeNotifierTests : IDisposable
{
    private readonly string _dir;
    private readonly string _other;

    public FileSystemChangeNotifierTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        _dir = Path.Combine(baseDir, "первая");
        _other = Path.Combine(baseDir, "вторая");
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(_other);
    }

    /// <summary>Дождаться сигнала или сказать, что его не было.</summary>
    private static async Task<bool> FiredAsync(TaskCompletionSource fired, int seconds = 5)
        => await Task.WhenAny(fired.Task, Task.Delay(TimeSpan.FromSeconds(seconds))) == fired.Task;

    [Fact]
    public async Task Raises_changed_when_file_is_written()
    {
        using var notifier = new FileSystemChangeNotifier([_dir], TimeSpan.FromMilliseconds(150));
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        notifier.Changed += () => fired.TrySetResult();
        notifier.Start();

        await File.WriteAllTextAsync(Path.Combine(_dir, "new.txt"), "hello");

        Assert.True(await FiredAsync(fired), "Сигнал Changed не пришёл после записи файла.");
    }

    [Fact]
    public async Task Reconfigure_starts_watching_a_newly_added_folder()
    {
        // Долг с среза 23: новое хранилище раньше подхватывалось только после перезапуска.
        // См. docs/features/29-managing-what-exists.md.
        using var notifier = new FileSystemChangeNotifier([_dir], TimeSpan.FromMilliseconds(150));
        notifier.Start();

        notifier.Reconfigure([_dir, _other]);

        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        notifier.Changed += () => fired.TrySetResult();
        await File.WriteAllTextAsync(Path.Combine(_other, "new.txt"), "привет");

        Assert.True(await FiredAsync(fired), "Сигнал не пришёл из добавленной папки.");
    }

    [Fact]
    public async Task Reconfigure_stops_watching_a_removed_folder()
    {
        using var notifier = new FileSystemChangeNotifier([_dir, _other], TimeSpan.FromMilliseconds(150));
        notifier.Start();

        notifier.Reconfigure([_dir]);

        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        notifier.Changed += () => fired.TrySetResult();
        await File.WriteAllTextAsync(Path.Combine(_other, "new.txt"), "привет");

        Assert.False(await FiredAsync(fired, seconds: 2), "Из убранной папки сигнал приходить не должен.");
    }

    [Fact]
    public async Task Reconfigure_before_start_does_not_fire_early()
    {
        using var notifier = new FileSystemChangeNotifier([_dir], TimeSpan.FromMilliseconds(150));
        notifier.Reconfigure([_dir, _other]);

        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        notifier.Changed += () => fired.TrySetResult();
        await File.WriteAllTextAsync(Path.Combine(_other, "new.txt"), "привет");

        Assert.False(await FiredAsync(fired, seconds: 2), "До Start() наблюдение не ведётся.");
    }

    public void Dispose()
    {
        string baseDir = Path.GetDirectoryName(_dir)!;
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, recursive: true);
    }
}
