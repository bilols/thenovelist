using System;
using System.IO;
using Novelist.OutlineBuilder;
using Spectre.Console;

namespace Novelist.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            ShowHelp();
            return 0;
        }

        var entity = args[0].ToLowerInvariant();
        var verb   = args[1].ToLowerInvariant();

        return (entity, verb) switch
        {
            ("outline", "create")         => RunOutlineCreate(args[2..]),
            ("outline", "expand-premise") => RunExpandPremise(args[2..]),
            ("outline", "define-arc")     => RunDefineArc(args[2..]),
            _                             => ShowHelp()
        };
    }

    // ------------------------------------------------------------------ create
    private static int RunOutlineCreate(string[] args)
    {
        if (!TryGetArg(args, "--project", out var projectPath))
            return Error("--project is required.");

        TryGetArg(args, "--output", out var outputDir);

        var schemaDir = Path.Combine(Directory.GetCurrentDirectory(), "tools");
        var presetDir = Path.Combine(Directory.GetCurrentDirectory(), "author_presets");

        try
        {
            var builder = new OutlineBuilderService(schemaDir, presetDir);
            var path    = builder.CreateOutlineAsync(projectPath, outputDir).GetAwaiter().GetResult();
            return Success($"Outline created: {path}");
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    // ------------------------------------------------------------ expand-premise
    private static int RunExpandPremise(string[] args)
    {
        if (!TryGetArg(args, "--outline", out var outlinePath))
            return Error("--outline is required.");

        var model = GetArgOrDefault(args, "--model", "gpt-4o-mini");

        var expander = new PremiseExpanderService(new NoOpLlmClient());
        try
        {
            expander.ExpandPremiseAsync(outlinePath, model).GetAwaiter().GetResult();
            return Success("Premise expanded.");
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    // ---------------------------------------------------------------- define-arc
    private static int RunDefineArc(string[] args)
    {
        if (!TryGetArg(args, "--outline", out var outlinePath))
            return Error("--outline is required.");

        var model = GetArgOrDefault(args, "--model", "gpt-4o-mini");

        var definer = new ArcDefinerService(new NoOpLlmClient());
        try
        {
            definer.DefineArcAsync(outlinePath, model).GetAwaiter().GetResult();
            return Success("Story arc defined.");
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    // ---------------------------------------------------------------- helpers
    private static bool TryGetArg(string[] args, string flag, out string value)
    {
        value = "";
        for (var i = 0; i < args.Length; i++)
            if (args[i] == flag && i + 1 < args.Length)
            { value = args[i + 1]; return true; }
        return false;
    }

    private static string GetArgOrDefault(string[] args, string flag, string def)
        => TryGetArg(args, flag, out var v) ? v : def;

    private static int Success(string msg)
    {
        AnsiConsole.MarkupLine($"[green]{msg}[/]");
        return 0;
    }

    private static int Error(string msg)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {msg}");
        return 1;
    }

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Novelist CLI[/]");
        AnsiConsole.MarkupLine(" outline create         --project <file> [--output <dir>]");
        AnsiConsole.MarkupLine(" outline expand-premise --outline <file> [--model <id>]");
        AnsiConsole.MarkupLine(" outline define-arc     --outline <file> [--model <id>]");
        return 0;
    }

    // ---------------------------------------------------------------- stub LLM
    private sealed class NoOpLlmClient : ILlmClient
    {
        public System.Threading.Tasks.Task<string> CompleteAsync(
            string prompt, string modelId, System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(
                "Act 1: Setup paragraph.\n\nAct 2: Confrontation paragraph.\n\nAct 3: Resolution paragraph.");
    }
}
