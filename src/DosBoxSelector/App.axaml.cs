using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DosBoxSelector.ViewModels;
using DosBoxSelector.Views;

namespace DosBoxSelector;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // --manage opens the flavor editor on its own, for editing the list outside
            // of a right-click. Otherwise the picker is the main window.
            desktop.MainWindow = Program.ManageOnly
                ? new FlavorEditorWindow { DataContext = new FlavorEditorViewModel() }
                : new PickerWindow { DataContext = new PickerViewModel(Program.TargetDirectory) };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
