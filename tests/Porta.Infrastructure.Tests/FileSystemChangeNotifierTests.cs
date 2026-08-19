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

    public FileSystemChangeNotifierTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "porta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public async Task Raises_changed_when_file_is_written()
    {
        using var notifier = new FileSystemChangeNotifier([_dir], TimeSpan.FromMilliseconds(150));
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        notifier.Changed += () => fired.TrySetResult();
        notifier.Start();

        await File.WriteAllTextAsync(Path.Combine(_dir, "new.txt"), "hello");

        Task completed = await Task.WhenAny(fired.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == fired.Task, "Сигнал Changed не пришёл после записи файла.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
