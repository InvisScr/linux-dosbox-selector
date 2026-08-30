using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DosBoxSelector.ViewModels;

namespace DosBoxSelector.Views;

public partial class FlavorEditorWindow : Window
{
    public FlavorEditorWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private FlavorEditorViewModel? Model => DataContext as FlavorEditorViewModel;

    private void OnAddClick(object? sender, RoutedEventArgs e) => Model?.Add();

    private void OnDuplicateClick(object? sender, RoutedEventArgs e) => Model?.Duplicate();

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => Model?.Remove();

    private void OnTestClick(object? sender, RoutedEventArgs e) => Model?.Test();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Model?.Save();
        Close();
    }
}
