using System.Diagnostics;
using Musiq.Application.Abstractions;

namespace Musiq.Infrastructure.Scanning;

public sealed class FileSystemLibraryScanner : ILibraryScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma", ".aiff", ".ape"
    };

    private readonly ISongRepository _songs;
    private readonly IAudioFileMetadataReader _metadataReader;

    public FileSystemLibraryScanner(ISongRepository songs, IAudioFileMetadataReader metadataReader)
    {
        _songs = songs;
        _metadataReader = metadataReader;
    }

    public async Task<LibraryScanResult> ScanAsync(
        string rootPath,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        var fullRoot = Path.GetFullPath(rootPath);

        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException(fullRoot);

        var files = Directory.EnumerateFiles(
                fullRoot,
                "*.*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                })
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .ToList();

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = Path.GetFullPath(path);
            existing.Add(normalized);

            var info = new FileInfo(normalized);
            var existingSong = await _songs.GetByPathAsync(normalized, cancellationToken);

            if (existingSong is null ||
                existingSong.FileSize != info.Length ||
                existingSong.LastModifiedUtc != info.LastWriteTimeUtc)
            {
                var song = await _metadataReader.ReadAsync(normalized, StableId(normalized), cancellationToken);
                await _songs.UpsertAsync(song, cancellationToken);
            }

            processed++;
            progress?.Report(new LibraryScanProgress(files.Count, processed, normalized));
        }

        var removed = await _songs.RemoveMissingFilesAsync(existing, fullRoot, cancellationToken);

        return new LibraryScanResult(
            fullRoot,
            files.Count,
            processed,
            removed,
            Stopwatch.GetElapsedTime(started));
    }

    private static long StableId(string path)
    {
        unchecked
        {
            const long offset = 1469598103934665603L;
            const long prime = 1099511628211L;
            long hash = offset;

            foreach (var c in path.ToUpperInvariant())
            {
                hash ^= c;
                hash *= prime;
            }

            return hash == 0 ? 1 : hash;
        }
    }
}
