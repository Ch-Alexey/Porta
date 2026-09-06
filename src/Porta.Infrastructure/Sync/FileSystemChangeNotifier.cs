using Porta.Core.Sync;

namespace Porta.Infrastructure.Sync;

/// <summary>
/// Наблюдает за папками хранилищ через <see cref="FileSystemWatcher"/> и поднимает один
/// дебаунснутый сигнал <see cref="Changed"/> после паузы без событий.
/// См. docs/features/23-file-change-sync.md.
/// </summary>
public sealed class FileSystemChangeNotifier : IChangeNotifier, IWatchedFolders, IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _started;

    public event Action? Changed;

    public FileSystemChangeNotifier(IEnumerable<string> folders, TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        _debounce = debounce ?? TimeSpan.FromSeconds(2);
        Rebuild(folders);
    }

    /// <summary>Начать наблюдение.</summary>
    public void Start()
    {
        lock (_gate)
        {
            _started = true;
            foreach (FileSystemWatcher watcher in _watchers)
                watcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>
    /// Наблюдать ровно за этими папками. Зовётся, когда пользователь добавил или удалил
    /// хранилище: иначе новое хранилище подхватилось бы только после перезапуска.
    /// </summary>
    public void Reconfigure(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        lock (_gate)
        {
            foreach (FileSystemWatcher watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();

            Rebuild(folders);
            if (!_started)
                return;

            foreach (FileSystemWatcher watcher in _watchers)
                watcher.EnableRaisingEvents = true;
        }
    }

    private void Rebuild(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
                continue;

            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Changed += OnFileSystemEvent;
            watcher.Created += OnFileSystemEvent;
            watcher.Deleted += OnFileSystemEvent;
            watcher.Renamed += OnFileSystemEvent;
            _watchers.Add(watcher);
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        lock (_gate)
        {
            _timer ??= new Timer(_ => Changed?.Invoke());
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (FileSystemWatcher watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
            _timer?.Dispose();
        }
    }
}
