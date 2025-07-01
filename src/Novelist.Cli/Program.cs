using System;
using System.IO;
using Novelist.OutlineBuilder;
using Spectre.Console;

namespace Novelist.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0].Equals("outline", StringComparison.OrdinalIgnoreCase))
        {
            var subCommand = args[1].ToLowerInvariant();

            switch (subCommand)
            {
                case "create":
                    return ExecuteOutlineCreate(args[2..]);
                default:
                    ShowHelp();
                    return 1;
            }
        }

        ShowHelp();
        return 0;
    }

    // ---------------------------------------------------------------------
    // outline create --project MyBook.project.json [--output ./mydir]
    // ---------------------------------------------------------------------
    private static int ExecuteOutlineCreate(string[] args)
    {
        string? projectPath = null;
        string? outputDir   = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project" when i + 1 < args.Length:
                    projectPath = args[++i];
                    break;

                case "--output" when i + 1 < args.Length:
                    outputDir = args[++i];
                    break;

                default:
                    AnsiConsole.MarkupLine($"[yellow]Ignoring unrecognised token:[/] {args[i]}");
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --project <file> is required.");
            return 1;
        }

        try
        {
            var schemaDir = Path.Combine(Directory.GetCurrentDirectory(), "tools");
            var presetDir = Path.Combine(Directory.GetCurrentDirectory(), "author_presets");

            var builder = new OutlineBuilderService(schemaDir, presetDir);
            var outlinePath = builder.CreateOutlineAsync(projectPath, outputDir).GetAwaiter().GetResult();

            AnsiConsole.MarkupLine($"[green]Outline successfully created:[/] {outlinePath}");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed:[/] {ex.Message}");
            return 1;
        }
    }

    // ---------------------------------------------------------------------
    // Basic CLI help
    // ---------------------------------------------------------------------
    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Novelist CLI[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[underline]Commands[/]");
        AnsiConsole.MarkupLine("  outline create --project <file> [--output <dir>]");
        AnsiConsole.WriteLine();
    }
}
