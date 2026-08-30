using DosBoxSelector.Models;

namespace DosBoxSelector.Services;

/// <summary>
/// Prints what the app would do for a folder, without opening a window. Useful when the
/// context-menu entry launches something unexpected and you want to see why.
/// </summary>
public static class Diagnostics
{
    public static int Run(string directory)
    {
        var flavors = FlavorStore.Load();
        var detector = new FlavorDetector();
        var confs = ConfScanner.Scan(directory, flavors);

        Console.WriteLine($"Folder:  {directory}");
        Console.WriteLine($"Config:  {FlavorStore.ConfigPath}");
        Console.WriteLine();

        Console.WriteLine("Config files found:");
        if (confs.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var conf in confs)
            {
                var owner = conf.FlavorId is null
                    ? "unrecognised"
                    : $"{conf.FlavorId} (by {conf.Source.ToString().ToLowerInvariant()})";
                Console.WriteLine($"  {conf.FileName,-28} -> {owner}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("Flavors:");
        foreach (var flavor in flavors)
        {
            var detection = detector.Detect(flavor);
            var mark = detection.Available ? "OK     " : "MISSING";
            Console.WriteLine($"  [{mark}] {flavor.DisplayName}");
            Console.WriteLine($"            {detection.Detail}");
            if (detection.ExecutablePath is not null)
                Console.WriteLine($"            path: {detection.ExecutablePath}");

            // Mirror the picker's default: this flavor's own files if any named it, otherwise
            // the whole set, in the order they appear on disk.
            var owned = confs.Where(c => c.FlavorId == flavor.Id).Select(c => c.Path).ToList();
            var chosen = owned.Count > 0 ? owned : confs.Select(c => c.Path).ToList();

            var plan = LaunchService.BuildPlan(flavor, detection, directory, chosen);
            Console.WriteLine($"            would run: {plan.Display}");
            Console.WriteLine();
        }

        return 0;
    }
}
