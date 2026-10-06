using Musiq.Domain.Entities;
using Musiq.Infrastructure.Database;
using Musiq.Infrastructure.Library;
using Xunit;

namespace Musiq.Infrastructure.Tests;

public sealed class LibraryDatabaseTests
{
    [Fact]
    public async Task Database_round_trips_song()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteSongRepository(database);
            var song = new Song(
                42,
                Path.Combine(root, "Artist - Song.mp3"),
                "Song",
                "Artist",
                "Album",
                TimeSpan.FromMinutes(3))
            {
                AlbumArtist = "Artist",
                Genre = "Rock",
                FileSize = 1234,
                LastModifiedUtc = DateTimeOffset.UtcNow
            };

            await repository.UpsertAsync(song);

            var result = await repository.GetRecentlyAddedAsync(10);

            var saved = Assert.Single(result);
            Assert.Equal(song.Path, saved.Path);
            Assert.Equal(song.Title, saved.Title);
            Assert.Equal(song.Artist, saved.Artist);
            Assert.Equal(song.Duration, saved.Duration);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
