using System;
using System.IO;
using Novelist.OutlineBuilder;
using Spectre.Console;

namespace Novelist.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2) return ShowHelp();

        return (args[0].ToLowerInvariant(), args[1].ToLowerInvariant()) switch
        {
            ("outline", "create")            => RunCreate(args[2..]),
            ("outline", "expand-premise")    => RunExpand(args[2..]),
            ("outline", "define-arc")        => RunArc(args[2..]),
            ("outline", "define-characters") => RunCharacters(args[2..]),
            _                                => ShowHelp()
        };
    }

    // ---------------------------------------------------------------- outline create
    private static int RunCreate(string[] a)
    {
        if (!TryGet(a, "--project", out var project)) return Error("--project is required.");
        TryGet(a, "--output", out var output);

        var schema = Path.Combine(Environment.CurrentDirectory, "tools");
        var preset = Path.Combine(Environment.CurrentDirectory, "author_presets");

        var path = new OutlineBuilderService(schema, preset)
                   .CreateOutlineAsync(project, output).Result;

        return Success($"Outline created: {path}");
    }

    // ----------------------------------------------------------- expand premise
    private static int RunExpand(string[] a)
    {
        if (!TryGet(a, "--outline", out var outline))  return Error("--outline is required.");
        var model = Get(a, "--model", "gpt-4o-mini");

        new PremiseExpanderService(new NoOpLlm())
            .ExpandPremiseAsync(outline, model).Wait();

        return Success("Premise expanded.");
    }

    // --------------------------------------------------------------- define arc
    private static int RunArc(string[] a)
    {
        if (!TryGet(a, "--outline", out var outline))  return Error("--outline is required.");
        var model = Get(a, "--model", "gpt-4o-mini");

        new ArcDefinerService(new NoOpLlm())
            .DefineArcAsync(outline, model).Wait();

        return Success("Story arc defined.");
    }

    // ------------------------------------------------------ define characters
    private static int RunCharacters(string[] a)
    {
        if (!TryGet(a, "--outline", out var outline))  return Error("--outline is required.");
        var model = Get(a, "--model", "gpt-4o-mini");

        new CharactersOutlinerService(new NoOpLlm())
            .DefineCharactersAsync(outline, model).Wait();

        return Success("Characters outlined.");
    }

    // ------------------------------------------------ helpers
    private static bool TryGet(string[] a, string flag, out string val)
    {
        val = string.Empty;
        for (var i = 0; i < a.Length; i++)
            if (a[i] == flag && i + 1 < a.Length) { val = a[i + 1]; return true; }
        return false;
    }
    private static string Get(string[] a, string flag, string d)
        => TryGet(a, flag, out var v) ? v : d;

    private static int Success(string m) { AnsiConsole.MarkupLine($"[green]{m}[/]"); return 0; }
    private static int Error  (string m) { AnsiConsole.MarkupLine($"[red]{m}[/]");  return 1; }
    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Novelist CLI[/]");
        AnsiConsole.MarkupLine(" outline create            --project <file> [--output <dir>]");
        AnsiConsole.MarkupLine(" outline expand-premise     --outline <file> [--model <id>]");
        AnsiConsole.MarkupLine(" outline define-arc         --outline <file> [--model <id>]");
        AnsiConsole.MarkupLine(" outline define-characters  --outline <file> [--model <id>]");
        return 0;
    }

    // dummy LLM
    private sealed class NoOpLlm : ILlmClient
    {
        public System.Threading.Tasks.Task<string> CompleteAsync(string p, string m,
            System.Threading.CancellationToken _ = default) =>
            System.Threading.Tasks.Task.FromResult(
                "[{\"name\":\"Jack Carpenter\",\"role\":\"Protagonist\",\"traits\":[\"haunted\",\"skilled\"],\"arc\":\"Faces the ghosts of his past to find redemption.\"}]");
    }
}
