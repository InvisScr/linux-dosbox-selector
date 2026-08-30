using CommunityToolkit.Mvvm.ComponentModel;
using DosBoxSelector.Models;

namespace DosBoxSelector.ViewModels;

/// <summary>
/// One <c>.conf</c> in the picker's ordered list: whether it will be passed to the emulator, and
/// where it sits in the sequence. Order is meaningful — every fork applies configs left to right,
/// so a later file overrides an earlier one.
/// </summary>
public sealed partial class ConfEntry : ObservableObject
{
    public ConfEntry(ConfMatch match, string? flavorDisplayName)
    {
        Path = match.Path;
        FileName = match.FileName;
        FlavorId = match.FlavorId;
        Note = flavorDisplayName is null
            ? "not recognised"
            : $"for {flavorDisplayName}"
              + (match.Source == ConfMatchSource.Content ? "" : " (by name)");
    }

    public string Path { get; }

    public string FileName { get; }

    public string? FlavorId { get; }

    /// <summary>Which flavor claimed this file, or that nothing did.</summary>
    public string Note { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>1-based position among the *selected* files, or null when this one is excluded.</summary>
    [ObservableProperty]
    public partial int? Order { get; set; }

    public string OrderLabel => Order?.ToString() ?? "–";

    partial void OnOrderChanged(int? value) => OnPropertyChanged(nameof(OrderLabel));
}
