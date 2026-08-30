using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DosBoxSelector.Models;
using DosBoxSelector.Services;

namespace DosBoxSelector.ViewModels;

/// <summary>
/// Editable wrapper around a <see cref="DosBoxFlavor"/>. List-typed fields are surfaced as
/// semicolon-separated text so they fit in a single text box.
/// </summary>
public sealed partial class FlavorEditItem : ObservableObject
{
    public FlavorEditItem(DosBoxFlavor flavor)
    {
        Source = flavor;
        DisplayName = flavor.DisplayName;
        Enabled = flavor.Enabled;
        IsFlatpak = flavor.Kind == FlavorKind.Flatpak;
        Command = flavor.Command;
        ConfArgument = flavor.ConfArgument;
        ConfPatterns = string.Join("; ", flavor.ConfPatterns);
        VersionMatch = flavor.VersionMatch;
        SearchPaths = string.Join("; ", flavor.SearchPaths);
        MountAsDriveC = flavor.MountAsDriveC;
        ExtraArguments = flavor.ExtraArguments;
    }

    public DosBoxFlavor Source { get; }

    [ObservableProperty] public partial string DisplayName { get; set; }
    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial bool IsFlatpak { get; set; }
    [ObservableProperty] public partial string Command { get; set; }
    [ObservableProperty] public partial string ConfArgument { get; set; }
    [ObservableProperty] public partial string ConfPatterns { get; set; }
    [ObservableProperty] public partial string VersionMatch { get; set; }
    [ObservableProperty] public partial string SearchPaths { get; set; }
    [ObservableProperty] public partial bool MountAsDriveC { get; set; }
    [ObservableProperty] public partial string ExtraArguments { get; set; }

    /// <summary>Relabels the command box, since the field means something different per kind.</summary>
    public string CommandLabel => IsFlatpak ? "Flatpak app ID" : "Executable (name or full path)";

    partial void OnIsFlatpakChanged(bool value) => OnPropertyChanged(nameof(CommandLabel));

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(ListLabel));

    public string ListLabel => string.IsNullOrWhiteSpace(DisplayName) ? "(unnamed)" : DisplayName;

    /// <summary>Copies the edited values back onto the model.</summary>
    public DosBoxFlavor Commit()
    {
        Source.DisplayName = DisplayName.Trim();
        Source.Enabled = Enabled;
        Source.Kind = IsFlatpak ? FlavorKind.Flatpak : FlavorKind.Native;
        Source.Command = Command.Trim();
        Source.ConfArgument = ConfArgument.Trim();
        Source.ConfPatterns = SplitList(ConfPatterns);
        Source.VersionMatch = VersionMatch.Trim();
        Source.SearchPaths = SplitList(SearchPaths);
        Source.MountAsDriveC = MountAsDriveC;
        Source.ExtraArguments = ExtraArguments.Trim();

        // The cached absolute path belongs to the old command; let detection find it again.
        Source.ResolvedPath = null;
        return Source;
    }

    private static List<string> SplitList(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public sealed partial class FlavorEditorViewModel : ObservableObject
{
    private readonly FlavorDetector _detector = new();

    public FlavorEditorViewModel()
    {
        foreach (var flavor in FlavorStore.Load())
            Items.Add(new FlavorEditItem(flavor));

        SelectedItem = Items.FirstOrDefault();
    }

    public ObservableCollection<FlavorEditItem> Items { get; } = [];

    [ObservableProperty]
    public partial FlavorEditItem? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string? TestResult { get; set; }

    public bool HasTestResult => !string.IsNullOrEmpty(TestResult);

    partial void OnTestResultChanged(string? value) => OnPropertyChanged(nameof(HasTestResult));

    partial void OnSelectedItemChanged(FlavorEditItem? value)
    {
        TestResult = null;
        OnPropertyChanged(nameof(HasSelection));
    }

    public bool HasSelection => SelectedItem is not null;

    public void Add()
    {
        var item = new FlavorEditItem(new DosBoxFlavor
        {
            DisplayName = "New flavor",
            ConfArgument = "-conf",
        });
        Items.Add(item);
        SelectedItem = item;
    }

    public void Duplicate()
    {
        if (SelectedItem is null)
            return;

        var copy = SelectedItem.Commit().Clone();
        copy.DisplayName += " (copy)";
        var item = new FlavorEditItem(copy);
        Items.Add(item);
        SelectedItem = item;
    }

    public void Remove()
    {
        if (SelectedItem is null)
            return;

        var index = Items.IndexOf(SelectedItem);
        Items.Remove(SelectedItem);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    /// <summary>Runs detection against the in-progress entry so the user can see what it resolves to.</summary>
    public void Test()
    {
        if (SelectedItem is null)
            return;

        var probe = SelectedItem.Commit();
        _detector.Reset();
        var result = _detector.Detect(probe);

        TestResult = result.Available
            ? $"Found: {result.ExecutablePath}\n{result.Detail}"
            : $"Not found — {result.Detail}";
    }

    public void Save()
    {
        var flavors = Items.Select(i => i.Commit()).ToList();
        FlavorStore.Save(flavors);
    }
}
