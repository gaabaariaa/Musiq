namespace Musiq.Domain.Entities;

public sealed record Song(
    long Id,
    string Path,
    string Title,
    string Artist,
    string Album,
    TimeSpan Duration);
