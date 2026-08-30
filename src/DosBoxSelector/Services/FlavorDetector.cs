using System.Diagnostics;
using System.Text;
using DosBoxSelector.Models;

namespace DosBoxSelector.Services;

/// <summary>Outcome of probing one flavor.</summary>
public sealed record DetectionResult(bool Available, string? ExecutablePath, string Detail)
{
    public static DetectionResult Missing(string detail) => new(false, null, detail);
}

/// <summary>
/// Decides whether each flavor is actually installed, and resolves which binary belongs to which.
///
/// The interesting case on a typical Linux box: dosbox-staging's package installs its binary as
/// plain <c>/usr/bin/dosbox</c>, the same name vanilla DOSBox uses. Name lookup alone would let
/// both flavors claim it, so every native candidate is confirmed by running <c>-version</c> and
/// matching the output against <see cref="DosBoxFlavor.VersionMatch"/>.
/// </summary>
public sealed class FlavorDetector
{
    /// <summary>
    /// Probe flag. Deliberately single-dash: vanilla DOSBox 0.74-3 does not recognise
    /// <c>--help</c>/<c>--version</c>-style long options and responds to an unrecognised
    /// argument by *starting the emulator*. <c>-version</c> is understood by vanilla,
    /// Staging and DOSBox-X alike, and all three print and exit.
    /// </summary>
    private const string ProbeArgument = "-version";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, DetectionResult> _cache = new();

    /// <summary>Clears memoised results, e.g. after the user edits flavors.</summary>
    public void Reset() => _cache.Clear();

    public DetectionResult Detect(DosBoxFlavor flavor)
    {
        var key = CacheKey(flavor);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var result = flavor.Kind switch
        {
            FlavorKind.Flatpak => DetectFlatpak(flavor),
            _ => DetectNative(flavor),
        };

        _cache[key] = result;
        return result;
    }

    private static string CacheKey(DosBoxFlavor f) =>
        $"{f.Kind}|{f.Command}|{f.VersionMatch}|{string.Join(';', f.SearchPaths)}";

    private static DetectionResult DetectFlatpak(DosBoxFlavor flavor)
    {
        if (string.IsNullOrWhiteSpace(flavor.Command))
            return DetectionResult.Missing("No flatpak app ID set");

        var probe = Run("flatpak", ["info", flavor.Command], ProbeTimeout);
        if (probe is null)
            return DetectionResult.Missing("flatpak is not installed");

        return probe.Value.ExitCode == 0
            ? new DetectionResult(true, flavor.Command, $"Flatpak {flavor.Command}")
            : DetectionResult.Missing($"Flatpak {flavor.Command} is not installed");
    }

    private DetectionResult DetectNative(DosBoxFlavor flavor)
    {
        if (string.IsNullOrWhiteSpace(flavor.Command))
            return DetectionResult.Missing("No command set");

        foreach (var candidate in CandidateExecutables(flavor))
        {
            if (string.IsNullOrEmpty(flavor.VersionMatch))
                return new DetectionResult(true, candidate, candidate);

            var version = ProbeVersion(candidate);
            if (version is null)
                continue;

            if (version.Contains(flavor.VersionMatch, StringComparison.OrdinalIgnoreCase))
                return new DetectionResult(true, candidate, FirstLine(version));
        }

        return DetectionResult.Missing($"'{flavor.Command}' not found");
    }

    /// <summary>
    /// Every binary that might be this flavor, best first: an explicit absolute path, a previously
    /// resolved one, PATH entries, then the configured search globs (which is how an
    /// extracted-tarball build sitting in $HOME gets picked up).
    /// </summary>
    private static IEnumerable<string> CandidateExecutables(DosBoxFlavor flavor)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (Path.IsPathRooted(flavor.Command) && IsExecutable(flavor.Command) && seen.Add(flavor.Command))
            yield return flavor.Command;

        if (!string.IsNullOrEmpty(flavor.ResolvedPath)
            && IsExecutable(flavor.ResolvedPath)
            && seen.Add(flavor.ResolvedPath))
            yield return flavor.ResolvedPath;

        if (!Path.IsPathRooted(flavor.Command))
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                var full = Path.Combine(dir, flavor.Command);
                if (IsExecutable(full) && seen.Add(full))
                    yield return full;
            }
        }

        foreach (var pattern in flavor.SearchPaths)
        {
            foreach (var hit in ExpandGlob(pattern))
            {
                if (IsExecutable(hit) && seen.Add(hit))
                    yield return hit;
            }
        }
    }

    /// <summary>
    /// Expands <c>~</c> and a single directory-level wildcard, e.g. <c>~/DOSBox*/dosbox</c>.
    /// Deliberately simple — enough for the "which folder did I unpack it into" case.
    /// </summary>
    private static IEnumerable<string> ExpandGlob(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            yield break;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (pattern.StartsWith("~/", StringComparison.Ordinal))
            pattern = Path.Combine(home, pattern[2..]);

        if (!pattern.Contains('*'))
        {
            yield return pattern;
            yield break;
        }

        var dir = Path.GetDirectoryName(pattern);
        var leaf = Path.GetFileName(pattern);
        if (string.IsNullOrEmpty(dir))
            yield break;

        // The wildcard may be in the directory portion (~/DOSBox*/dosbox) or the leaf (dosbox*).
        var parent = Path.GetDirectoryName(dir);
        var dirLeaf = Path.GetFileName(dir);

        IEnumerable<string> directories;
        if (dir.Contains('*') && !string.IsNullOrEmpty(parent) && Directory.Exists(parent))
        {
            directories = SafeEnumerateDirectories(parent, dirLeaf);
        }
        else
        {
            directories = Directory.Exists(dir) ? [dir] : [];
        }

        foreach (var d in directories)
        {
            if (leaf.Contains('*'))
            {
                foreach (var f in SafeEnumerateFiles(d, leaf))
                    yield return f;
            }
            else
            {
                var candidate = Path.Combine(d, leaf);
                if (File.Exists(candidate))
                    yield return candidate;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string parent, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(parent, pattern);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string dir, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(dir, pattern);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            // Unix file modes are the only thing that matters here; this app targets Linux desktops.
            if (OperatingSystem.IsWindows())
                return true;

            var mode = File.GetUnixFileMode(path);
            return mode.HasFlag(UnixFileMode.UserExecute)
                   || mode.HasFlag(UnixFileMode.GroupExecute)
                   || mode.HasFlag(UnixFileMode.OtherExecute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string? ProbeVersion(string executable)
    {
        var probe = Run(executable, [ProbeArgument], ProbeTimeout);
        if (probe is null)
            return null;

        var text = probe.Value.Output;
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Runs a command and captures its output, killing the whole process tree if it overruns.
    /// The kill is not paranoia: a misconfigured flavor pointed at vanilla DOSBox with the
    /// wrong probe flag will open an emulator window and sit there forever.
    /// </summary>
    private static (int ExitCode, string Output)? Run(string fileName, string[] args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return null;

            // Close stdin immediately so nothing can block waiting for input.
            process.StandardInput.Close();

            var output = new StringBuilder();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKill(process);
                return null;
            }

            output.Append(stdout.GetAwaiter().GetResult());
            output.Append(stderr.GetAwaiter().GetResult());
            return (process.ExitCode, output.ToString());
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(2000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            // Already gone.
        }
    }

    private static string FirstLine(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                return trimmed;
        }
        return text.Trim();
    }
}
