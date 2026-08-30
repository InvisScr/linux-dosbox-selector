namespace DosBoxSelector.Services;

/// <summary>Works out which folder the picker is acting on.</summary>
public static class TargetPath
{
    /// <summary>
    /// Resolves the folder from the command line. Dolphin's <c>%u</c> arrives as a
    /// <c>file://</c> URI, but a plain path is accepted too so the app is usable from a shell.
    ///
    /// Returns null when an argument was supplied but could not be resolved. That case must not
    /// silently fall back to the working directory: doing so launches the emulator against a
    /// folder the user never picked.
    /// </summary>
    public static string? Resolve(string[] args)
    {
        var sawCandidate = false;

        foreach (var arg in args)
        {
            // Skip our own switches (--diagnose, --manage).
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
                continue;

            sawCandidate = true;

            foreach (var path in Interpretations(arg))
            {
                if (Directory.Exists(path))
                    return Path.GetFullPath(path);

                // Tolerate being handed a file inside the folder.
                if (File.Exists(path))
                    return Path.GetDirectoryName(Path.GetFullPath(path));
            }
        }

        if (sawCandidate)
            return null;

        var cwd = Environment.CurrentDirectory;
        return Directory.Exists(cwd) ? cwd : null;
    }

    /// <summary>
    /// The ways one argument might be meant, best first.
    /// </summary>
    private static IEnumerable<string> Interpretations(string arg)
    {
        var isUri = Uri.TryCreate(arg, UriKind.Absolute, out var uri) && uri.IsFile;

        if (isUri)
            yield return uri!.LocalPath;

        // A file:// URI whose path was never percent-encoded loses everything after the first
        // '#', which URI parsing treats as a fragment — and folder names really do contain '#'
        // (a "2026 C# Linux" directory is enough to trigger it). Decoding the remainder by hand
        // recovers the real path.
        if (isUri && arg.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            yield return Uri.UnescapeDataString(arg["file://".Length..]);

        // Not a URI at all, or a scheme we can't reach (smb://, sftp://, trash:/).
        if (!arg.Contains("://", StringComparison.Ordinal))
            yield return arg;
    }
}
