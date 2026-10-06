using System.Collections.Concurrent;
using Musiq.Application.Abstractions;

namespace Musiq.Infrastructure.Scanning;

public sealed class FileSystemLibraryChangeMonitor : ILibraryChangeMonitor, IDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma", ".aiff", ".ape"
    };

    private readonly ConcurrentDictionary<string, WatchState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounce;

    public FileSystemLibraryChangeMonitor(TimeSpan? debounce = null)
    {
        _debounce = debounce ?? TimeSpan.FromMilliseconds(500);
    }

    public event EventHandler<LibraryChangeDetectedEventArgs>? ChangesDetected;

    public void Watch(string rootPath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException(fullRoot);

        if (_states.ContainsKey(fullRoot))
            return;

        var watcher = new FileSystemWatcher(fullRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            Filter = "*.*",
            EnableRaisingEvents = true
        };

        var state = new WatchState(fullRoot, watcher, _debounce, RaiseChanges);
        watcher.Created += state.OnEvent;
        watcher.Changed += state.OnEvent;
        watcher.Deleted += state.OnEvent;
        watcher.Renamed += state.OnRenamed;

        if (!_states.TryAdd(fullRoot, state))
            state.Dispose();
    }

    public void Unwatch(string rootPath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        if (_states.TryRemove(fullRoot, out var state))
            state.Dispose();
    }

    private void RaiseChanges(string rootPath, IReadOnlyList<string> paths)
    {
        if (paths.Count > 0)
            ChangesDetected?.Invoke(this, new LibraryChangeDetectedEventArgs(rootPath, paths));
    }

    public void Dispose()
    {
        foreach (var pair in _states)
            pair.Value.Dispose();

        _states.Clear();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class WatchState : IDisposable
    {
        private readonly object _gate = new();
        private readonly string _rootPath;
        private readonly FileSystemWatcher _watcher;
        private readonly TimeSpan _debounce;
        private readonly Action<string, IReadOnlyList<string>> _callback;
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
        private Timer? _timer;

        public WatchState(
            string rootPath,
            FileSystemWatcher watcher,
            TimeSpan debounce,
            Action<string, IReadOnlyList<string>> callback)
        {
            _rootPath = rootPath;
            _watcher = watcher;
            _debounce = debounce;
            _callback = callback;
        }

        public void OnEvent(object sender, FileSystemEventArgs e)
        {
            AddPath(e.FullPath);
        }

        public void OnRenamed(object sender, RenamedEventArgs e)
        {
            AddPath(e.OldFullPath);
            AddPath(e.FullPath);
        }

        private void AddPath(string path)
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                return;

            lock (_gate)
            {
                _paths.Add(path);
                _timer ??= new Timer(Flush, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
            }
        }

        private void Flush(object? state)
        {
            string[] paths;
            lock (_gate)
            {
                paths = _paths.ToArray();
                _paths.Clear();
            }

            if (paths.Length > 0)
                _callback(_rootPath, paths);
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();

            lock (_gate)
            {
                _timer?.Dispose();
                _timer = null;
                _paths.Clear();
            }
        }
    }
}
