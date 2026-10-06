using Musiq.Infrastructure.Database;
using Musiq.Infrastructure.Library;
using Musiq.Infrastructure.Metadata;
using Musiq.Infrastructure.Scanning;
using Xunit;

namespace Musiq.Infrastructure.Tests;

public sealed class FileSystemLibraryScannerTests
{
    [Fact]
    public async Task Scanner_imports_supported_files_and_skips_unsupported_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusiqTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "Artist - Song.mp3"), [1, 2, 3]);
            await File.WriteAllBytesAsync(Path.Combine(root, "notes.txt"), [1, 2, 3]);

            var database = new LibraryDatabase(Path.Combine(root, "library.db"));
            await database.InitializeAsync();

            var repository = new SqliteSongRepository(database);
            var scanner = new FileSystemLibraryScanner(repository, new BasicAudioFileMetadataReader());

            var result = await scanner.ScanAsync(root);
            var songs = await repository.GetRecentlyAddedAsync(10);

            Assert.Equal(1, result.FilesDiscovered);
            Assert.Single(songs);
            Assert.Equal("Song", songs[0].Title);
            Assert.Equal("Artist", songs[0].Artist);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
