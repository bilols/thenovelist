using Spectre.Console;

namespace Novelist.Cli;

/// <summary>
/// Temporary entry point to unblock compilation.
/// Full command handling will be added in the next milestone.
/// </summary>
internal static class Program
{
    /// <summary>Console application entry.</summary>
    /// <param name="args">Command‑line arguments.</param>
    /// <returns>Exit code (0 = success).</returns>
    private static int Main(string[] args)
    {
        AnsiConsole.MarkupLine("[bold green]Novelist CLI[/]");
        AnsiConsole.MarkupLine("Placeholder entry point – command verbs will be implemented soon.");

        return 0;
    }
}
