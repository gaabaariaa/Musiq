using Microsoft.Data.Sqlite;

namespace Musiq.Infrastructure.Database;

public sealed class LibraryDatabase
{
    private readonly string _connectionString;

    public LibraryDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path is required.", nameof(databasePath));

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS Songs (
                Id INTEGER PRIMARY KEY,
                Path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL,
                Album TEXT NOT NULL,
                AlbumArtist TEXT NOT NULL,
                Genre TEXT NOT NULL,
                Year INTEGER NULL,
                TrackNumber INTEGER NULL,
                DiscNumber INTEGER NULL,
                Composer TEXT NOT NULL,
                Comment TEXT NOT NULL,
                Lyrics TEXT NOT NULL,
                DurationTicks INTEGER NOT NULL,
                FileSize INTEGER NOT NULL,
                LastModifiedUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Songs_Artist ON Songs(Artist);
            CREATE INDEX IF NOT EXISTS IX_Songs_Album ON Songs(Album);
            CREATE INDEX IF NOT EXISTS IX_Songs_LastModifiedUtc ON Songs(LastModifiedUtc);

            CREATE VIRTUAL TABLE IF NOT EXISTS SongsSearch USING fts5(
                Title,
                Artist,
                Album,
                AlbumArtist,
                Genre,
                Composer,
                Comment,
                Lyrics
            );

            CREATE TRIGGER IF NOT EXISTS TR_SongsSearch_Insert
            AFTER INSERT ON Songs
            BEGIN
                INSERT INTO SongsSearch(rowid, Title, Artist, Album, AlbumArtist, Genre, Composer, Comment, Lyrics)
                VALUES (
                    new.Id, new.Title, new.Artist, new.Album, new.AlbumArtist,
                    new.Genre, new.Composer, new.Comment, new.Lyrics
                );
            END;

            CREATE TRIGGER IF NOT EXISTS TR_SongsSearch_Update
            AFTER UPDATE ON Songs
            BEGIN
                DELETE FROM SongsSearch WHERE rowid = old.Id;
                INSERT INTO SongsSearch(rowid, Title, Artist, Album, AlbumArtist, Genre, Composer, Comment, Lyrics)
                VALUES (
                    new.Id, new.Title, new.Artist, new.Album, new.AlbumArtist,
                    new.Genre, new.Composer, new.Comment, new.Lyrics
                );
            END;

            CREATE TRIGGER IF NOT EXISTS TR_SongsSearch_Delete
            AFTER DELETE ON Songs
            BEGIN
                DELETE FROM SongsSearch WHERE rowid = old.Id;
            END;

            INSERT OR REPLACE INTO SongsSearch(rowid, Title, Artist, Album, AlbumArtist, Genre, Composer, Comment, Lyrics)
            SELECT Id, Title, Artist, Album, AlbumArtist, Genre, Composer, Comment, Lyrics
            FROM Songs
            WHERE Id NOT IN (SELECT rowid FROM SongsSearch);

            CREATE TABLE IF NOT EXISTS ScanLocations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                LastScanUtc TEXT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
