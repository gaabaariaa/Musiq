using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface ISongRepository
{
    Task<Song?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, SongFileState>> GetFileStatesAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default);
    Task UpsertAsync(Song song, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default);
    Task<int> RemoveMissingFilesAsync(
        IReadOnlyCollection<string> existingPaths,
        string rootPath,
        CancellationToken cancellationToken = default);
}
