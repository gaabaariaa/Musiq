namespace Musiq.Application.Abstractions;

public sealed record SongFileState(
    string Path,
    long FileSize,
    DateTimeOffset LastModifiedUtc);
