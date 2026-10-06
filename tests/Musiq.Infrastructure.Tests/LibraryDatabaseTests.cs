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

    [Fact]
    public async Task Bulk_file_state_lookup_returns_only_registered_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteSongRepository(database);
            var existingPath = Path.Combine(root, "Artist - Song.mp3");
            var missingPath = Path.Combine(root, "Missing.mp3");
            var modified = DateTimeOffset.UtcNow.AddMinutes(-5);

            await repository.UpsertAsync(new Song(
                42,
                existingPath,
                "Song",
                "Artist",
                "Album",
                TimeSpan.FromMinutes(3))
            {
                FileSize = 1234,
                LastModifiedUtc = modified
            });

            var states = await repository.GetFileStatesAsync(
                new[] { existingPath, missingPath });

            var state = Assert.Single(states);
            Assert.True(states.ContainsKey(existingPath));
            Assert.False(states.ContainsKey(missingPath));
            Assert.Equal(existingPath, state.Key);
            Assert.Equal(1234, state.Value.FileSize);
            Assert.Equal(modified, state.Value.LastModifiedUtc);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Library_query_supports_paging_and_full_text_search()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteSongRepository(database);
            await repository.UpsertBatchAsync(new[]
            {
                new Song(1, Path.Combine(root, "one.mp3"), "First Light", "Artist One", "Album", TimeSpan.FromMinutes(3))
                {
                    Lyrics = "hello moon"
                },
                new Song(2, Path.Combine(root, "two.mp3"), "Night Drive", "Artist Two", "Album", TimeSpan.FromMinutes(3)),
                new Song(3, Path.Combine(root, "three.mp3"), "Light Years", "Artist Three", "Album", TimeSpan.FromMinutes(3))
            });

            var page = await repository.GetPageAsync(2, 1);
            var pagedSong = Assert.Single(page);
            Assert.Equal(2, pagedSong.Id);

            var search = await repository.SearchAsync("light", 1, 10);
            Assert.Equal(2, search.Count);
            Assert.Equal(new[] { 1L, 3L }, search.Select(song => song.Id).OrderBy(id => id));

            search = await repository.SearchAsync("Artist Two", 1, 10);
            Assert.Single(search);
            Assert.Equal(2, search[0].Id);

            search = await repository.SearchAsync("moon", 1, 10);
            Assert.Single(search);
            Assert.Equal(1, search[0].Id);

            await repository.UpsertAsync(new Song(
                1,
                Path.Combine(root, "one.mp3"),
                "First Light Remastered",
                "Artist One",
                "Album",
                TimeSpan.FromMinutes(3)));

            search = await repository.SearchAsync("remastered", 1, 10);
            Assert.Single(search);
            Assert.Equal(1, search[0].Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Library_query_rejects_invalid_page_arguments()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteSongRepository(database);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => repository.GetPageAsync(0, 10));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => repository.GetPageAsync(1, 0));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => repository.SearchAsync("test", 1, 501));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Remove_missing_files_only_removes_files_under_scan_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        var otherRoot = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(otherRoot);

        try
        {
            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteSongRepository(database);
            var keptPath = Path.Combine(root, "Kept.mp3");
            var removedPath = Path.Combine(root, "Removed.mp3");
            var outsidePath = Path.Combine(otherRoot, "Outside.mp3");

            await repository.UpsertBatchAsync(new[]
            {
                new Song(1, keptPath, "Kept", "Artist", "Album", TimeSpan.FromMinutes(3)),
                new Song(2, removedPath, "Removed", "Artist", "Album", TimeSpan.FromMinutes(3)),
                new Song(3, outsidePath, "Outside", "Artist", "Album", TimeSpan.FromMinutes(3))
            });

            var removed = await repository.RemoveMissingFilesAsync(
                new[] { keptPath },
                root);

            Assert.Equal(1, removed);
            Assert.NotNull(await repository.GetByPathAsync(keptPath));
            Assert.Null(await repository.GetByPathAsync(removedPath));
            Assert.NotNull(await repository.GetByPathAsync(outsidePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(otherRoot, recursive: true);
        }
    }
}
