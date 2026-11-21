using System.Diagnostics;
using DosBoxSelector.Models;

namespace DosBoxSelector.Services;

/// <summary>The exact command that will be run, for launching and for showing the user.</summary>
public sealed record LaunchPlan(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    /// <summary>Human-readable rendering, quoted only where needed. Display only — never executed.</summary>
    public string Display =>
        string.Join(' ', new[] { FileName }.Concat(Arguments).Select(Quote));

    private static string Quote(string s) =>
        s.Length > 0 && !s.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'')
            ? s
            : '"' + s.Replace("\"", "\\\"") + '"';
}

/// <summary>Builds and starts the emulator command line.</summary>
public static class LaunchService
{
    private const string DosBoxXFlatpakId = "com.dosbox_x.DOSBox-X";

    /// <summary>
    /// Builds the command line. <paramref name="confPaths"/> is applied in order: every fork
    /// accepts its config flag more than once and lets later files override earlier ones
    /// (verified against DOSBox 0.74-3, Staging 0.82.2 and DOSBox-X 2026.08.02), which is what
    /// makes split configs — a base file plus an override, as GOG ships — work.
    /// </summary>
    public static LaunchPlan BuildPlan(
        DosBoxFlavor flavor,
        DetectionResult detection,
        string directory,
        IReadOnlyList<string> confPaths)
    {
        var args = new List<string>();
        var fileName = detection.ExecutablePath ?? flavor.Command;

        if (flavor.Kind == FlavorKind.Flatpak)
        {
            fileName = "flatpak";
            args.Add("run");

            // The DOSBox-X flatpak ships with filesystems=home, so anything outside $HOME —
            // a second drive, /mnt, an external disk — is invisible inside the sandbox and the
            // mount silently fails. Granting just this directory for just this run keeps the
            // app's standing permissions untouched.
            args.Add($"--filesystem={directory}");

            // Start inside the sandbox in the mounted folder rather than relying on flatpak to
            // carry the host working directory across, which it silently drops to $HOME when
            // the path isn't visible.
            args.Add($"--cwd={directory}");

            // The DOSBox-X flatpak's default command is a wrapper script that does `cd "$HOME"`
            // before exec'ing the emulator, which throws away --cwd and breaks every relative
            // MOUNT/IMGMOUNT in a game's config ("Directory .\Foo does not exist"). Run the
            // binary directly instead, passing the one flag the wrapper exists to add.
            var isDosBoxX = string.Equals(flavor.Command, DosBoxXFlatpakId, StringComparison.OrdinalIgnoreCase);
            if (isDosBoxX)
                args.Add("--command=dosbox-x");

            args.Add(flavor.Command);

            if (isDosBoxX)
                args.Add("-nopromptfolder");
        }

        // Omitted entirely when nothing is chosen, so the emulator falls back to its own defaults.
        if (!string.IsNullOrWhiteSpace(flavor.ConfArgument))
        {
            foreach (var confPath in confPaths)
            {
                if (string.IsNullOrEmpty(confPath))
                    continue;

                args.Add(flavor.ConfArgument);
                args.Add(confPath);
            }
        }

        if (flavor.MountAsDriveC)
        {
            args.Add("-c");
            args.Add($"MOUNT C \"{directory}\"");
            args.Add("-c");
            args.Add("C:");
        }

        // {conf} expands to the last config in the sequence — the one whose settings win.
        var effectiveConf = confPaths.LastOrDefault(p => !string.IsNullOrEmpty(p));
        foreach (var extra in SplitArguments(Expand(flavor.ExtraArguments, directory, effectiveConf)))
            args.Add(extra);

        return new LaunchPlan(fileName, args, directory);
    }

    /// <summary>Starts the emulator detached. Throws on failure so the caller can show the reason.</summary>
    public static void Launch(LaunchPlan plan)
    {
        if (!Directory.Exists(plan.WorkingDirectory))
            throw new DirectoryNotFoundException($"Folder no longer exists: {plan.WorkingDirectory}");

        // Move the selector itself into the folder being mounted before anything is started,
        // so the emulator — and any wrapper script standing in for it — begins there and
        // relative paths in configs and -c commands resolve against the game folder.
        Environment.CurrentDirectory = plan.WorkingDirectory;

        var psi = new ProcessStartInfo
        {
            FileName = plan.FileName,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
        };

        // ArgumentList, never a single command string: these paths routinely contain spaces,
        // exclamation marks and quotes, and there is no shell in the loop to blame.
        foreach (var arg in plan.Arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start {plan.FileName}.");
    }

    private static string Expand(string template, string directory, string? confPath) =>
        template
            .Replace("{dir}", directory, StringComparison.Ordinal)
            .Replace("{conf}", confPath ?? "", StringComparison.Ordinal);

    /// <summary>
    /// Splits a free-form argument string on whitespace, honouring single and double quotes so a
    /// user can write <c>-c "MOUNT D /some/path"</c> in the extra-arguments box.
    /// </summary>
    internal static List<string> SplitArguments(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
            return result;

        var current = new System.Text.StringBuilder();
        var quote = '\0';
        var hasToken = false;

        foreach (var c in input)
        {
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                else
                    current.Append(c);
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                hasToken = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
                continue;
            }

            current.Append(c);
            hasToken = true;
        }

        if (hasToken)
            result.Add(current.ToString());

        return result;
    }
}
