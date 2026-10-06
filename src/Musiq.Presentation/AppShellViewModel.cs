using CommunityToolkit.Mvvm.ComponentModel;

namespace Musiq.Presentation;

public sealed partial class AppShellViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Musiq";
}
