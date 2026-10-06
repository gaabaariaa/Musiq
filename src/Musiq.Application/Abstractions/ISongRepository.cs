using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface ISongRepository
{
    Task<Song?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    Task UpsertAsync(Song song, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default);
    Task<int> RemoveMissingFilesAsync(IReadOnlySet<string> existingPaths, string rootPath, CancellationToken cancellationToken = default);
}
