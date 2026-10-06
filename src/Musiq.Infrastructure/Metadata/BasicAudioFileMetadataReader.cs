using Musiq.Application.Abstractions;
using Musiq.Domain.Entities;

namespace Musiq.Infrastructure.Metadata;

public sealed class BasicAudioFileMetadataReader : IAudioFileMetadataReader
{
    public Task<Song> ReadAsync(string path, long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = new FileInfo(path);
        var title = Path.GetFileNameWithoutExtension(path);
        var separator = title.IndexOf(" - ", StringComparison.Ordinal);
        var artist = string.Empty;

        if (separator > 0)
        {
            artist = title[..separator].Trim();
            title = title[(separator + 3)..].Trim();
        }

        return Task.FromResult(new Song(
            id,
            info.FullName,
            title,
            artist,
            string.Empty,
            TimeSpan.Zero)
        {
            FileSize = info.Length,
            LastModifiedUtc = info.LastWriteTimeUtc
        });
    }
}
