namespace Musiq.Application.Abstractions;

public interface ILibraryChangeMonitor : IAsyncDisposable
{
    void Watch(string rootPath);
    void Unwatch(string rootPath);
    event EventHandler<LibraryChangeDetectedEventArgs>? ChangesDetected;
}

public sealed class LibraryChangeDetectedEventArgs : EventArgs
{
    public LibraryChangeDetectedEventArgs(string rootPath, IReadOnlyList<string> paths)
    {
        RootPath = rootPath;
        Paths = paths;
    }

    public string RootPath { get; }
    public IReadOnlyList<string> Paths { get; }
}
