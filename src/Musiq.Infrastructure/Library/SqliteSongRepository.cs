using Microsoft.Data.Sqlite;
using Musiq.Application.Abstractions;
using Musiq.Domain.Entities;

namespace Musiq.Infrastructure.Library;

public sealed class SqliteSongRepository : ISongRepository, ILibraryQuery
{
    private readonly Database.LibraryDatabase _database;

    public SqliteSongRepository(Database.LibraryDatabase database)
    {
        _database = database;
    }

    public async Task UpsertAsync(Song song, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Songs
            (Id, Path, Title, Artist, Album, AlbumArtist, Genre, Year, TrackNumber, DiscNumber,
             Composer, Comment, Lyrics, DurationTicks, FileSize, LastModifiedUtc)
            VALUES
            ($id, $path, $title, $artist, $album, $albumArtist, $genre, $year, $trackNumber, $discNumber,
             $composer, $comment, $lyrics, $durationTicks, $fileSize, $lastModifiedUtc)
            ON CONFLICT(Path) DO UPDATE SET
                Title = excluded.Title,
                Artist = excluded.Artist,
                Album = excluded.Album,
                AlbumArtist = excluded.AlbumArtist,
                Genre = excluded.Genre,
                Year = excluded.Year,
                TrackNumber = excluded.TrackNumber,
                DiscNumber = excluded.DiscNumber,
                Composer = excluded.Composer,
                Comment = excluded.Comment,
                Lyrics = excluded.Lyrics,
                DurationTicks = excluded.DurationTicks,
                FileSize = excluded.FileSize,
                LastModifiedUtc = excluded.LastModifiedUtc;
            """;

        command.Parameters.AddWithValue("$id", song.Id);
        command.Parameters.AddWithValue("$path", song.Path);
        command.Parameters.AddWithValue("$title", song.Title);
        command.Parameters.AddWithValue("$artist", song.Artist);
        command.Parameters.AddWithValue("$album", song.Album);
        command.Parameters.AddWithValue("$albumArtist", song.AlbumArtist);
        command.Parameters.AddWithValue("$genre", song.Genre);
        command.Parameters.AddWithValue("$year", (object?)song.Year ?? DBNull.Value);
        command.Parameters.AddWithValue("$trackNumber", (object?)song.TrackNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$discNumber", (object?)song.DiscNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$composer", song.Composer);
        command.Parameters.AddWithValue("$comment", song.Comment);
        command.Parameters.AddWithValue("$lyrics", song.Lyrics);
        command.Parameters.AddWithValue("$durationTicks", song.Duration.Ticks);
        command.Parameters.AddWithValue("$fileSize", song.FileSize);
        command.Parameters.AddWithValue("$lastModifiedUtc", song.LastModifiedUtc.UtcDateTime.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveMissingFilesAsync(IReadOnlySet<string> existingPaths, string rootPath, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var prefix = Path.GetFullPath(rootPath);
        if (!prefix.EndsWith(Path.DirectorySeparatorChar))
            prefix += Path.DirectorySeparatorChar;

        command.CommandText = """
            DELETE FROM Songs
            WHERE Path LIKE $rootPrefix || '%'
              AND Path NOT IN (SELECT value FROM json_each($paths));
            """;
        command.Parameters.AddWithValue("$rootPrefix", prefix);
        command.Parameters.AddWithValue("$paths", System.Text.Json.JsonSerializer.Serialize(existingPaths));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> GetRecentlyAddedAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            return Array.Empty<Song>();

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, Title, Artist, Album, AlbumArtist, Genre, Year,
                   TrackNumber, DiscNumber, Composer, Comment, Lyrics,
                   DurationTicks, FileSize, LastModifiedUtc
            FROM Songs
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var songs = new List<Song>(Math.Min(limit, 256));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            songs.Add(new Song(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                TimeSpan.FromTicks(reader.GetInt64(13)))
            {
                AlbumArtist = reader.GetString(5),
                Genre = reader.GetString(6),
                Year = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                TrackNumber = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                DiscNumber = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                Composer = reader.GetString(10),
                Comment = reader.GetString(11),
                Lyrics = reader.GetString(12),
                FileSize = reader.GetInt64(14),
                LastModifiedUtc = DateTimeOffset.Parse(reader.GetString(15))
            });
        }

        return songs;
    }
}
