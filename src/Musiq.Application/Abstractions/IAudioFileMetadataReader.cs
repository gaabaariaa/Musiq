using Musiq.Domain.Entities;

namespace Musiq.Application.Abstractions;

public interface IAudioFileMetadataReader
{
    Task<Song> ReadAsync(string path, long id, CancellationToken cancellationToken = default);
}
