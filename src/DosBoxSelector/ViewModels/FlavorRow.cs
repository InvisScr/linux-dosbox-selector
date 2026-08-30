using CommunityToolkit.Mvvm.ComponentModel;
using DosBoxSelector.Models;
using DosBoxSelector.Services;

namespace DosBoxSelector.ViewModels;

/// <summary>One row in the picker: a flavor plus what detection and conf-scanning found out about it.</summary>
public sealed partial class FlavorRow : ObservableObject
{
    public FlavorRow(DosBoxFlavor flavor, DetectionResult detection)
    {
        Flavor = flavor;
        Detection = detection;
    }

    public DosBoxFlavor Flavor { get; }

    public DetectionResult Detection { get; }

    public string DisplayName => Flavor.DisplayName;

    public bool IsAvailable => Detection.Available;

    /// <summary>Version banner when found, reason when not.</summary>
    public string StatusText => Detection.Detail;

    /// <summary>Greys out flavors that aren't installed rather than hiding them, so the list stays stable.</summary>
    public double NameOpacity => IsAvailable ? 1.0 : 0.45;

    /// <summary>Set when a <c>.conf</c> in the folder points at this flavor, e.g. "matches dosbox-x.conf".</summary>
    [ObservableProperty]
    public partial string? MatchHint { get; set; }

    public bool HasMatchHint => !string.IsNullOrEmpty(MatchHint);

    partial void OnMatchHintChanged(string? value) => OnPropertyChanged(nameof(HasMatchHint));
}
