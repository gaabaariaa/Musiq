using Microsoft.Data.Sqlite;
using Musiq.Application.Abstractions;
using Musiq.Domain.Entities;

namespace Musiq.Infrastructure.Library;

public sealed class SqliteSongRepository : ISongRepository, ILibraryQuery
{
    private readonly Database.LibraryDatabase _database;

    public SqliteSongRepository(Database.LibraryDatabase database) => _database = database;

    public async Task<Song?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(path);
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, Artist, Album, DurationTicks, FileSize, LastModifiedUtc FROM Songs WHERE Path = $path LIMIT 1;";
        command.Parameters.AddWithValue("$path", normalized);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new Song(reader.GetInt64(0), normalized, reader.GetString(1), reader.GetString(2), reader.GetString(3), TimeSpan.FromTicks(reader.GetInt64(4)))
        {
            FileSize = reader.GetInt64(5),
            LastModifiedUtc = DateTimeOffset.Parse(reader.GetString(6))
        };
    }

    public async Task<IReadOnlyDictionary<string, SongFileState>> GetFileStatesAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        if (paths.Count == 0)
            return new Dictionary<string, SongFileState>(StringComparer.OrdinalIgnoreCase);

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var create = connection.CreateCommand();
        create.Transaction = (SqliteTransaction)transaction;
        create.CommandText = "CREATE TEMP TABLE ScanPaths (Path TEXT PRIMARY KEY COLLATE NOCASE);";
        await create.ExecuteNonQueryAsync(cancellationToken);

        await using var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = "INSERT OR IGNORE INTO ScanPaths(Path) VALUES ($path);";
        var parameter = insert.Parameters.Add("$path", SqliteType.Text);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            parameter.Value = Path.GetFullPath(path);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var query = connection.CreateCommand();
        query.Transaction = (SqliteTransaction)transaction;
        query.CommandText = """
            SELECT s.Path, s.FileSize, s.LastModifiedUtc
            FROM Songs s
            INNER JOIN ScanPaths p ON p.Path = s.Path;
            """;

        var states = new Dictionary<string, SongFileState>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var path = reader.GetString(0);
            states[path] = new SongFileState(
                path,
                reader.GetInt64(1),
                DateTimeOffset.Parse(reader.GetString(2)));
        }

        await transaction.CommitAsync(cancellationToken);
        return states;
    }

    public async Task UpsertAsync(Song song, CancellationToken cancellationToken = default)
        => await UpsertBatchAsync(new[] { song }, cancellationToken);

    public async Task UpsertBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default)
    {
        if (songs.Count == 0) return;

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO Songs
            (Id, Path, Title, Artist, Album, AlbumArtist, Genre, Year, TrackNumber, DiscNumber,
             Composer, Comment, Lyrics, DurationTicks, FileSize, LastModifiedUtc)
            VALUES
            ($id, $path, $title, $artist, $album, $albumArtist, $genre, $year, $trackNumber, $discNumber,
             $composer, $comment, $lyrics, $durationTicks, $fileSize, $lastModifiedUtc)
            ON CONFLICT(Path) DO UPDATE SET
                Title = excluded.Title, Artist = excluded.Artist, Album = excluded.Album,
                AlbumArtist = excluded.AlbumArtist, Genre = excluded.Genre, Year = excluded.Year,
                TrackNumber = excluded.TrackNumber, DiscNumber = excluded.DiscNumber,
                Composer = excluded.Composer, Comment = excluded.Comment, Lyrics = excluded.Lyrics,
                DurationTicks = excluded.DurationTicks, FileSize = excluded.FileSize,
                LastModifiedUtc = excluded.LastModifiedUtc;
            """;

        var p = new Dictionary<string, SqliteParameter>
        {
            ["$id"] = command.Parameters.Add("$id", SqliteType.Integer),
            ["$path"] = command.Parameters.Add("$path", SqliteType.Text),
            ["$title"] = command.Parameters.Add("$title", SqliteType.Text),
            ["$artist"] = command.Parameters.Add("$artist", SqliteType.Text),
            ["$album"] = command.Parameters.Add("$album", SqliteType.Text),
            ["$albumArtist"] = command.Parameters.Add("$albumArtist", SqliteType.Text),
            ["$genre"] = command.Parameters.Add("$genre", SqliteType.Text),
            ["$year"] = command.Parameters.Add("$year", SqliteType.Integer),
            ["$trackNumber"] = command.Parameters.Add("$trackNumber", SqliteType.Integer),
            ["$discNumber"] = command.Parameters.Add("$discNumber", SqliteType.Integer),
            ["$composer"] = command.Parameters.Add("$composer", SqliteType.Text),
            ["$comment"] = command.Parameters.Add("$comment", SqliteType.Text),
            ["$lyrics"] = command.Parameters.Add("$lyrics", SqliteType.Text),
            ["$durationTicks"] = command.Parameters.Add("$durationTicks", SqliteType.Integer),
            ["$fileSize"] = command.Parameters.Add("$fileSize", SqliteType.Integer),
            ["$lastModifiedUtc"] = command.Parameters.Add("$lastModifiedUtc", SqliteType.Text)
        };

        foreach (var song in songs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            p["$id"].Value = song.Id;
            p["$path"].Value = song.Path;
            p["$title"].Value = song.Title;
            p["$artist"].Value = song.Artist;
            p["$album"].Value = song.Album;
            p["$albumArtist"].Value = song.AlbumArtist;
            p["$genre"].Value = song.Genre;
            p["$year"].Value = (object?)song.Year ?? DBNull.Value;
            p["$trackNumber"].Value = (object?)song.TrackNumber ?? DBNull.Value;
            p["$discNumber"].Value = (object?)song.DiscNumber ?? DBNull.Value;
            p["$composer"].Value = song.Composer;
            p["$comment"].Value = song.Comment;
            p["$lyrics"].Value = song.Lyrics;
            p["$durationTicks"].Value = song.Duration.Ticks;
            p["$fileSize"].Value = song.FileSize;
            p["$lastModifiedUtc"].Value = song.LastModifiedUtc.UtcDateTime.ToString("O");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> RemoveMissingFilesAsync(
        IReadOnlyCollection<string> existingPaths,
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var prefix = Path.GetFullPath(rootPath);
        if (!prefix.EndsWith(Path.DirectorySeparatorChar))
            prefix += Path.DirectorySeparatorChar;

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var create = connection.CreateCommand();
        create.Transaction = (SqliteTransaction)transaction;
        create.CommandText = "CREATE TEMP TABLE ExistingScanPaths (Path TEXT PRIMARY KEY COLLATE NOCASE);";
        await create.ExecuteNonQueryAsync(cancellationToken);

        await using var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = "INSERT OR IGNORE INTO ExistingScanPaths(Path) VALUES ($path);";
        var pathParameter = insert.Parameters.Add("$path", SqliteType.Text);

        foreach (var path in existingPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pathParameter.Value = Path.GetFullPath(path);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var delete = connection.CreateCommand();
        delete.Transaction = (SqliteTransaction)transaction;
        delete.CommandText = """
            DELETE FROM Songs
            WHERE substr(Path, 1, length($rootPrefix)) = $rootPrefix
              AND NOT EXISTS (
                  SELECT 1
                  FROM ExistingScanPaths p
                  WHERE p.Path = Songs.Path
              );
            """;
        delete.Parameters.AddWithValue("$rootPrefix", prefix);

        var removed = await delete.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return removed;
    }

    public async Task<IReadOnlyList<Song>> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, Title, Artist, Album, AlbumArtist, Genre, Year,
                   TrackNumber, DiscNumber, Composer, Comment, Lyrics,
                   DurationTicks, FileSize, LastModifiedUtc
            FROM Songs
            ORDER BY Id
            LIMIT $pageSize OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$pageSize", pageSize);
        command.Parameters.AddWithValue("$offset", checked((page - 1) * pageSize));

        return await ReadSongsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        if (string.IsNullOrWhiteSpace(query))
            return await GetPageAsync(page, pageSize, cancellationToken);

        var matchQuery = BuildFtsQuery(query);
        if (matchQuery.Length == 0)
            return Array.Empty<Song>();

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.Id, s.Path, s.Title, s.Artist, s.Album, s.AlbumArtist, s.Genre, s.Year,
                   s.TrackNumber, s.DiscNumber, s.Composer, s.Comment, s.Lyrics,
                   s.DurationTicks, s.FileSize, s.LastModifiedUtc
            FROM SongsSearch search
            INNER JOIN Songs s ON s.Id = search.rowid
            WHERE SongsSearch MATCH $query
            ORDER BY bm25(SongsSearch), s.Id
            LIMIT $pageSize OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$query", matchQuery);
        command.Parameters.AddWithValue("$pageSize", pageSize);
        command.Parameters.AddWithValue("$offset", checked((page - 1) * pageSize));

        return await ReadSongsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> GetRecentlyAddedAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Array.Empty<Song>();

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
            songs.Add(new Song(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), TimeSpan.FromTicks(reader.GetInt64(13)))
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
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private static string BuildFtsQuery(string query)
    {
        var terms = query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.Replace(""", """"))
            .Where(term => term.Length > 0)
            .Select(term => $""{term}"*");

        return string.Join(" AND ", terms);
    }

    private static async Task<IReadOnlyList<Song>> ReadSongsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var songs = new List<Song>();
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
