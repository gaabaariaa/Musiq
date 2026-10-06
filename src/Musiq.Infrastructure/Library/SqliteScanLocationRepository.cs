using Microsoft.Data.Sqlite;
using Musiq.Application.Abstractions;
using Musiq.Domain.Entities;

namespace Musiq.Infrastructure.Library;

public sealed class SqliteScanLocationRepository : IScanLocationRepository
{
    private readonly Database.LibraryDatabase _database;

    public SqliteScanLocationRepository(Database.LibraryDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<ScanLocation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Path, IsEnabled, LastScanUtc
            FROM ScanLocations
            ORDER BY Path COLLATE NOCASE;
            """;

        var locations = new List<ScanLocation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            locations.Add(new ScanLocation(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt64(2) != 0,
                reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3))));
        }

        return locations;
    }

    public async Task<ScanLocation> AddAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(path);

        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ScanLocations (Path, IsEnabled)
            VALUES ($path, 1)
            ON CONFLICT(Path) DO UPDATE SET IsEnabled = 1
            RETURNING Id, Path, IsEnabled, LastScanUtc;
            """;
        command.Parameters.AddWithValue("$path", normalized);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Failed to create scan location.");

        return new ScanLocation(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetInt64(2) != 0,
            reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3)));
    }

    public async Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScanLocations SET IsEnabled = $enabled WHERE Id = $id;";
        command.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ScanLocations WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkScannedAsync(long id, DateTimeOffset scannedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScanLocations SET LastScanUtc = $lastScanUtc WHERE Id = $id;";
        command.Parameters.AddWithValue("$lastScanUtc", scannedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
