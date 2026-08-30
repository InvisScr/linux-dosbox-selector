using System.Text.Json.Serialization;

namespace DosBoxSelector.Models;

/// <summary>How a flavor is invoked. Determines how <see cref="DosBoxFlavor.Command"/> is read.</summary>
public enum FlavorKind
{
    /// <summary>A native executable: a bare name resolved via PATH, or an absolute path.</summary>
    Native,

    /// <summary>A flatpak application id, launched via <c>flatpak run</c>.</summary>
    Flatpak,
}

/// <summary>
/// A user-editable DOSBox variant. Everything needed to detect and launch a fork lives here,
/// so adding a new one is pure data — no code changes.
/// </summary>
public sealed class DosBoxFlavor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    public string DisplayName { get; set; } = "New flavor";

    public bool Enabled { get; set; } = true;

    public FlavorKind Kind { get; set; } = FlavorKind.Native;

    /// <summary>Executable name or absolute path (Native), or flatpak app id (Flatpak).</summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// Flag introducing a config file, e.g. <c>-conf</c> or <c>--conf</c>. Omitted entirely
    /// when the user launches without a config.
    /// </summary>
    public string ConfArgument { get; set; } = "-conf";

    /// <summary>Filename globs this flavor claims, most specific first (e.g. <c>dosbox-x.conf</c>).</summary>
    public List<string> ConfPatterns { get; set; } = new();

    /// <summary>
    /// Substring expected in <c>-version</c> output. Used to resolve the case where several
    /// flavors nominate the same binary name — notably dosbox-staging installing as
    /// plain <c>/usr/bin/dosbox</c>. Empty disables the check.
    /// </summary>
    public string VersionMatch { get; set; } = "";

    /// <summary>Extra places to look when <see cref="Command"/> is not on PATH. Globs, <c>~</c> expanded.</summary>
    public List<string> SearchPaths { get; set; } = new();

    /// <summary>Append <c>-c "MOUNT C &lt;dir&gt;" -c "C:"</c> so the folder opens at a DOS prompt.</summary>
    public bool MountAsDriveC { get; set; } = true;

    /// <summary>Free-form extra arguments. Supports <c>{dir}</c> and <c>{conf}</c> placeholders.</summary>
    public string ExtraArguments { get; set; } = "";

    /// <summary>
    /// Absolute path cached by the detector once a Native command has been located via
    /// <see cref="SearchPaths"/>, so the glob only runs once.
    /// </summary>
    public string? ResolvedPath { get; set; }

    public DosBoxFlavor Clone() => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        DisplayName = DisplayName,
        Enabled = Enabled,
        Kind = Kind,
        Command = Command,
        ConfArgument = ConfArgument,
        ConfPatterns = new List<string>(ConfPatterns),
        VersionMatch = VersionMatch,
        SearchPaths = new List<string>(SearchPaths),
        MountAsDriveC = MountAsDriveC,
        ExtraArguments = ExtraArguments,
        ResolvedPath = null,
    };
}

/// <summary>Root of <c>flavors.json</c>. <see cref="Version"/> exists so the schema can migrate later.</summary>
public sealed class FlavorFile
{
    public int Version { get; set; } = 1;

    public List<DosBoxFlavor> Flavors { get; set; } = new();
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(FlavorFile))]
public partial class FlavorJsonContext : JsonSerializerContext;
