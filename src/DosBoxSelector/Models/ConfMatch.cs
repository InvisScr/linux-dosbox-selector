namespace DosBoxSelector.Models;

/// <summary>How a <c>.conf</c> file was tied to a flavor. Content beats filename.</summary>
public enum ConfMatchSource
{
    /// <summary>Nothing claimed it. Still offered in the dropdown, just doesn't drive pre-selection.</summary>
    None,

    /// <summary>A filename glob from <see cref="DosBoxFlavor.ConfPatterns"/> matched.</summary>
    FileName,

    /// <summary>The file's own contents named the flavor. Authoritative.</summary>
    Content,
}

/// <summary>A <c>.conf</c> found next to the target folder, and the flavor it points at (if any).</summary>
public sealed record ConfMatch(
    string Path,
    string FileName,
    string? FlavorId,
    ConfMatchSource Source)
{
    public bool IsMatched => FlavorId is not null;
}
