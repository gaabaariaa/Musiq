using Musiq.Domain.Entities;

namespace Musiq.Domain.Tests;

public sealed class SongTests
{
    [Fact]
    public void Song_keeps_core_identity()
    {
        var song = new Song(1, "a.mp3", "Title", "Artist", "Album", TimeSpan.FromMinutes(3));
        Assert.Equal("Title", song.Title);
        Assert.Equal("Artist", song.Artist);
        Assert.Equal("Album", song.Album);
    }
}
