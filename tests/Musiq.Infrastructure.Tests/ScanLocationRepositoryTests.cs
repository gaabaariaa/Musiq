using Musiq.Infrastructure.Database;
using Musiq.Infrastructure.Library;
using Xunit;

namespace Musiq.Infrastructure.Tests;

public sealed class ScanLocationRepositoryTests
{
    [Fact]
    public async Task Scan_locations_can_be_added_enabled_disabled_and_removed()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteScanLocationRepository(database);
            var location = await repository.AddAsync(Path.Combine(root, "Music"));

            Assert.True(location.IsEnabled);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "Music")), location.Path);

            await repository.SetEnabledAsync(location.Id, false);
            var disabled = Assert.Single(await repository.GetAllAsync());
            Assert.False(disabled.IsEnabled);

            var scannedAt = DateTimeOffset.UtcNow;
            await repository.MarkScannedAsync(location.Id, scannedAt);
            var scanned = Assert.Single(await repository.GetAllAsync());
            Assert.NotNull(scanned.LastScanUtc);

            await repository.RemoveAsync(location.Id);
            Assert.Empty(await repository.GetAllAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
