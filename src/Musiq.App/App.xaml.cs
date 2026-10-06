using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Musiq.Application.Abstractions;
using Musiq.Infrastructure.Library;

namespace Musiq.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var services = new ServiceCollection();
        services.AddSingleton<ILibraryQuery, InMemoryLibraryQuery>();
        _services = services.BuildServiceProvider();
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
