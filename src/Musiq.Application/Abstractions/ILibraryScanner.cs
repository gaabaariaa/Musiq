namespace Musiq.Application.Abstractions;

public interface ILibraryScanner
{
    Task<LibraryScanResult> ScanAsync(
        string rootPath,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record LibraryScanProgress(
    int FilesDiscovered,
    int FilesProcessed,
    string? CurrentPath);

public sealed record LibraryScanResult(
    string RootPath,
    int FilesDiscovered,
    int FilesImported,
    int FilesRemoved,
    TimeSpan Duration);
