using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Musiq.Application.Abstractions;
using Musiq.Infrastructure.Database;
using Musiq.Infrastructure.Library;
using Musiq.Infrastructure.Metadata;
using Musiq.Infrastructure.Scanning;

namespace Musiq.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Musiq");

        services.AddSingleton(new LibraryDatabase(Path.Combine(dataDirectory, "library.db")));
        services.AddSingleton<SqliteSongRepository>();
        services.AddSingleton<ISongRepository>(sp => sp.GetRequiredService<SqliteSongRepository>());
        services.AddSingleton<ILibraryQuery>(sp => sp.GetRequiredService<SqliteSongRepository>());
        services.AddSingleton<IAudioFileMetadataReader, BasicAudioFileMetadataReader>();
        services.AddSingleton<ILibraryScanner, FileSystemLibraryScanner>();
        services.AddSingleton<ILibraryChangeMonitor, FileSystemLibraryChangeMonitor>();

        _services = services.BuildServiceProvider();

        InitializeDatabaseAsync().ConfigureAwait(false);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _services!.GetRequiredService<LibraryDatabase>().InitializeAsync();
        }
        catch (Exception)
        {
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
