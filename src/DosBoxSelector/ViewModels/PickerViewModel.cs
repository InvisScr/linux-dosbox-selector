using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DosBoxSelector.Models;
using DosBoxSelector.Services;

namespace DosBoxSelector.ViewModels;

public sealed partial class PickerViewModel : ObservableObject
{
    private readonly FlavorDetector _detector = new();
    private List<DosBoxFlavor> _flavors;

    public PickerViewModel(string targetDirectory)
    {
        TargetDirectory = targetDirectory;
        _flavors = FlavorStore.Load();
        Refresh();
    }

    public string TargetDirectory { get; }

    public string TargetName
    {
        get
        {
            var name = System.IO.Path.GetFileName(TargetDirectory.TrimEnd('/'));
            return string.IsNullOrEmpty(name) ? TargetDirectory : name;
        }
    }

    public ObservableCollection<FlavorRow> Flavors { get; } = [];

    /// <summary>Every <c>.conf</c> in the folder, in the order they will be applied.</summary>
    public ObservableCollection<ConfEntry> Confs { get; } = [];

    public bool HasConfs => Confs.Count > 0;

    /// <summary>Ticked files, in list order. This is exactly what gets passed to the emulator.</summary>
    public IReadOnlyList<string> SelectedConfPaths =>
        Confs.Where(c => c.IsSelected).Select(c => c.Path).ToList();

    [ObservableProperty]
    public partial FlavorRow? SelectedFlavor { get; set; }

    [ObservableProperty]
    public partial ConfEntry? SelectedConfEntry { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnSelectedFlavorChanged(FlavorRow? value)
    {
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(CommandPreview));
    }

    partial void OnSelectedConfEntryChanged(ConfEntry? value)
    {
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    /// <summary>A flavor that isn't installed can be selected in the list but not launched.</summary>
    public bool CanLaunch => SelectedFlavor is { IsAvailable: true };

    public bool CanMoveUp => SelectedConfEntry is not null && Confs.IndexOf(SelectedConfEntry) > 0;

    public bool CanMoveDown =>
        SelectedConfEntry is not null && Confs.IndexOf(SelectedConfEntry) < Confs.Count - 1;

    /// <summary>The exact command line, shown so there is never any doubt about what will run.</summary>
    public string CommandPreview
    {
        get
        {
            if (SelectedFlavor is null)
                return "";

            var plan = LaunchService.BuildPlan(
                SelectedFlavor.Flavor, SelectedFlavor.Detection, TargetDirectory, SelectedConfPaths);
            return plan.Display;
        }
    }

    /// <summary>Moves the highlighted config one place earlier in the sequence.</summary>
    public void MoveConfUp() => MoveSelectedConf(-1);

    /// <summary>Moves the highlighted config one place later in the sequence.</summary>
    public void MoveConfDown() => MoveSelectedConf(+1);

    private void MoveSelectedConf(int delta)
    {
        if (SelectedConfEntry is null)
            return;

        var from = Confs.IndexOf(SelectedConfEntry);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= Confs.Count)
            return;

        Confs.Move(from, to);
        RenumberConfs();

        // Move() drops the ListBox selection; put it back on the item the user is dragging around.
        SelectedConfEntry = Confs[to];
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
    }

    /// <summary>Recomputes the 1..n badges and refreshes the preview after any change.</summary>
    private void RenumberConfs()
    {
        var position = 0;
        foreach (var entry in Confs)
            entry.Order = entry.IsSelected ? ++position : null;

        OnPropertyChanged(nameof(CommandPreview));
    }

    /// <summary>Re-runs detection and conf scanning. Called at startup and after the editor closes.</summary>
    public void Refresh()
    {
        _flavors = FlavorStore.Load();
        _detector.Reset();

        var confs = ConfScanner.Scan(TargetDirectory, _flavors);

        Flavors.Clear();
        foreach (var flavor in _flavors.Where(f => f.Enabled))
        {
            var detection = _detector.Detect(flavor);

            // Persist a binary found by search-path glob, so the next run skips the filesystem walk.
            if (detection.Available
                && flavor.Kind == FlavorKind.Native
                && detection.ExecutablePath is { } exe
                && flavor.ResolvedPath != exe)
            {
                flavor.ResolvedPath = exe;
                FlavorStore.Save(_flavors);
            }

            var row = new FlavorRow(flavor, detection);
            var match = confs.FirstOrDefault(c => c.FlavorId == flavor.Id);
            if (match is not null)
                row.MatchHint = $"matches {match.FileName}";

            Flavors.Add(row);
        }

        foreach (var old in Confs)
            old.PropertyChanged -= OnConfEntryChanged;
        Confs.Clear();

        foreach (var conf in confs)
        {
            var owner = _flavors.FirstOrDefault(f => f.Id == conf.FlavorId)?.DisplayName;
            var entry = new ConfEntry(conf, owner);
            entry.PropertyChanged += OnConfEntryChanged;
            Confs.Add(entry);
        }

        OnPropertyChanged(nameof(HasConfs));
        SelectPreferred(confs);
    }

    /// <summary>Ticking or unticking a file changes both the sequence numbering and the preview.</summary>
    private void OnConfEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConfEntry.IsSelected))
            RenumberConfs();
    }

    /// <summary>
    /// Pre-selects whatever the folder implies: the flavor a config was written for, and which
    /// files to feed it. Falls back to the first installed flavor so Enter always does something
    /// sensible. Everything here is a starting point — the user can retick and reorder freely.
    /// </summary>
    private void SelectPreferred(IReadOnlyList<ConfMatch> confs)
    {
        // Content matches are authoritative, so prefer them over filename guesses.
        var best = confs.Where(c => c.IsMatched).OrderByDescending(c => c.Source).FirstOrDefault();

        if (best is not null)
        {
            var row = Flavors.FirstOrDefault(f => f.Flavor.Id == best.FlavorId && f.IsAvailable)
                      ?? Flavors.FirstOrDefault(f => f.Flavor.Id == best.FlavorId);
            if (row is not null)
                SelectedFlavor = row;
        }

        SelectedFlavor ??= Flavors.FirstOrDefault(f => f.IsAvailable) ?? Flavors.FirstOrDefault();

        var flavorId = SelectedFlavor?.Flavor.Id;
        var ownedByFlavor = Confs.Where(c => c.FlavorId is not null && c.FlavorId == flavorId).ToList();

        if (ownedByFlavor.Count > 0)
        {
            // Some files name this flavor: take those and leave the rest alone.
            foreach (var entry in ownedByFlavor)
                entry.IsSelected = true;
        }
        else
        {
            // Nothing identified itself. Hand-written configs — the split base+override pairs that
            // GOG ships, for instance — carry no generator header, so the whole set is very likely
            // meant to be applied together. Tick everything and let the preview show the result.
            foreach (var entry in Confs)
                entry.IsSelected = true;
        }

        RenumberConfs();
        SelectedConfEntry = Confs.FirstOrDefault(c => c.IsSelected) ?? Confs.FirstOrDefault();
    }

    /// <summary>Starts the emulator. Returns true when the picker should close.</summary>
    public bool TryLaunch()
    {
        if (SelectedFlavor is not { IsAvailable: true } row)
        {
            ErrorMessage = "That flavor isn't installed. Pick another, or fix it in Manage flavors.";
            return false;
        }

        try
        {
            var plan = LaunchService.BuildPlan(
                row.Flavor, row.Detection, TargetDirectory, SelectedConfPaths);
            LaunchService.Launch(plan);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not launch {row.DisplayName}: {ex.Message}";
            return false;
        }
    }
}
