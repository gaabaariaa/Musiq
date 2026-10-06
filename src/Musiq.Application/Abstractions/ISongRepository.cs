using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface ISongRepository
{
    Task UpsertAsync(Song song, CancellationToken cancellationToken = default);
    Task RemoveMissingFilesAsync(IReadOnlySet<string> existingPaths, string rootPath, CancellationToken cancellationToken = default);
}
