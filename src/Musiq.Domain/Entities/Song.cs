namespace Musiq.Domain.Entities;

public sealed record Song(
    long Id,
    string Path,
    string Title,
    string Artist,
    string Album,
    TimeSpan Duration)
{
    public string AlbumArtist { get; init; } = Artist;
    public string Genre { get; init; } = string.Empty;
    public int? Year { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string Composer { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string Lyrics { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTimeOffset LastModifiedUtc { get; init; }
}
