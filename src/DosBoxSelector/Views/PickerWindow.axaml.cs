using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DosBoxSelector.ViewModels;

namespace DosBoxSelector.Views;

public partial class PickerWindow : Window
{
    public PickerWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private PickerViewModel? Model => DataContext as PickerViewModel;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void OnLaunchClick(object? sender, RoutedEventArgs e) => Launch();

    private void OnFlavorDoubleTapped(object? sender, TappedEventArgs e) => Launch();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnConfUpClick(object? sender, RoutedEventArgs e) => Model?.MoveConfUp();

    private void OnConfDownClick(object? sender, RoutedEventArgs e) => Model?.MoveConfDown();

    private async void OnManageClick(object? sender, RoutedEventArgs e)
    {
        var editor = new FlavorEditorWindow { DataContext = new FlavorEditorViewModel() };
        await editor.ShowDialog(this);

        // Flavors may have been added, renamed or repointed; re-detect and re-scan.
        Model?.Refresh();
    }

    private void Launch()
    {
        if (Model is null)
            return;

        // Only close on success — a failed launch leaves the window up with the reason showing.
        if (Model.TryLaunch())
            Close();
    }
}
