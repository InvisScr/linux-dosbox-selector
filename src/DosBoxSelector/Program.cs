using Avalonia;
using DosBoxSelector.Services;

namespace DosBoxSelector;

internal static class Program
{
    /// <summary>The folder the picker acts on, resolved from Dolphin's <c>%u</c> before the UI starts.</summary>
    public static string TargetDirectory { get; private set; } = Environment.CurrentDirectory;

    /// <summary>Set by <c>--manage</c>: open the flavor editor instead of the picker.</summary>
    public static bool ManageOnly { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        ManageOnly = args.Contains("--manage");

        var target = TargetPath.Resolve(args);
        if (target is null)
        {
            // Deliberately an error rather than a fallback to the working directory — running
            // the emulator against a folder the user did not choose is worse than doing nothing.
            Console.Error.WriteLine(
                "DOSBox Selector: could not resolve a local folder from the arguments.");
            Console.Error.WriteLine(
                "Pass a local directory or file:// URI, or run from inside the folder.");
            return 1;
        }

        TargetDirectory = target;

        // Headless report of what would be detected and launched, for troubleshooting.
        if (args.Contains("--diagnose"))
            return Diagnostics.Run(target);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
