using System.Text.Json;
using DosBoxSelector.Models;

namespace DosBoxSelector.Services;

/// <summary>Loads and saves the flavor list from <c>$XDG_CONFIG_HOME/dosbox-selector/flavors.json</c>.</summary>
public static class FlavorStore
{
    public static string ConfigDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var baseDir = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdg;
            return Path.Combine(baseDir, "dosbox-selector");
        }
    }

    public static string ConfigPath => Path.Combine(ConfigDirectory, "flavors.json");

    /// <summary>Reads the config, falling back to seeded defaults if it is missing or unreadable.</summary>
    public static List<DosBoxFlavor> Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var parsed = JsonSerializer.Deserialize(json, FlavorJsonContext.Default.FlavorFile);
                if (parsed?.Flavors is { Count: > 0 })
                    return parsed.Flavors;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable config must not stop the picker from opening.
        }

        var defaults = CreateDefaults();
        Save(defaults);
        return defaults;
    }

    public static void Save(IEnumerable<DosBoxFlavor> flavors)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var file = new FlavorFile { Version = 1, Flavors = flavors.ToList() };
            var json = JsonSerializer.Serialize(file, FlavorJsonContext.Default.FlavorFile);

            // Write via a temp file so an interrupted save can't truncate a good config.
            var temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, ConfigPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; the in-memory list still works for this session.
        }
    }

    /// <summary>
    /// The three flavors the app ships knowing about. Note that vanilla and Staging both
    /// nominate the bare command <c>dosbox</c> — Staging's RPM installs as <c>/usr/bin/dosbox</c>,
    /// so <see cref="DosBoxFlavor.VersionMatch"/> is what tells them apart, and vanilla's
    /// <see cref="DosBoxFlavor.SearchPaths"/> cover the common "extracted tarball in $HOME" layout.
    /// </summary>
    public static List<DosBoxFlavor> CreateDefaults() =>
    [
        new DosBoxFlavor
        {
            Id = "dosbox-vanilla",
            DisplayName = "DOSBox",
            Kind = FlavorKind.Native,
            Command = "dosbox",
            ConfArgument = "-conf",
            ConfPatterns = ["dosbox.conf", "dosbox-0.7*.conf"],
            VersionMatch = "DOSBox version",
            SearchPaths =
            [
                "~/DOSBox*/dosbox",
                "~/dosbox*/dosbox",
                "~/Applications/DOSBox*/dosbox",
                "/opt/dosbox*/dosbox",
                "/usr/local/bin/dosbox",
            ],
        },
        new DosBoxFlavor
        {
            Id = "dosbox-staging",
            DisplayName = "DOSBox Staging",
            Kind = FlavorKind.Native,
            Command = "dosbox",
            ConfArgument = "--conf",
            ConfPatterns = ["dosbox-staging.conf", "dosbox.conf"],
            VersionMatch = "dosbox-staging",
            SearchPaths = ["/usr/bin/dosbox", "/usr/local/bin/dosbox-staging"],
        },
        new DosBoxFlavor
        {
            Id = "dosbox-x",
            DisplayName = "DOSBox-X",
            Kind = FlavorKind.Flatpak,
            Command = "com.dosbox_x.DOSBox-X",
            ConfArgument = "-conf",
            ConfPatterns = ["dosbox-x.conf", "dosbox-x-*.conf"],
            VersionMatch = "DOSBox-X",
            SearchPaths = [],
        },
    ];
}
