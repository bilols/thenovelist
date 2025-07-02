using System;
using System.IO;
using Novelist.OutlineBuilder;
using Spectre.Console;

namespace Novelist.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2) return Help();

            // Detect --live flag early and strip it from arg list
            var live = false;
            var list = new System.Collections.Generic.List<string>(args);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is "--live" or "-l")
                {
                    live = true;
                    list.RemoveAt(i);
                }
            }
            args = list.ToArray();

            ILlmClient client;
            if (live)
            {
                var key = Environment.GetEnvironmentVariable("OPENAI_KEY");
                if (string.IsNullOrWhiteSpace(key))
                {
                    AnsiConsole.MarkupLine("[red]ERROR: --live specified but OPENAI_KEY not set.[/]");
                    return 1;
                }
                client = new OpenAiLlmClient();
                AnsiConsole.MarkupLine("[green]Using live OpenAI client.[/]");
            }
            else
            {
                client = new Stub();
            }

            return (args[0].ToLowerInvariant(), args[1].ToLowerInvariant()) switch
            {
                ("outline", "create")            => RunCreate(args[2..]),
                ("outline", "expand-premise")    => RunExpand(args[2..], client),
                ("outline", "define-arc")        => RunArc(args[2..], client),
                ("outline", "define-characters") => RunChars(args[2..], client),
                ("outline", "define-structure")  => RunStruct(args[2..], client),
                _                                => Help()
            };
        }

        // ---------------------------------------------------------------- create
        private static int RunCreate(string[] a)
        {
            if (!Try(a, "--project", out var proj)) return Err("--project required");
            Try(a, "--output", out var output);

            var schema = Path.Combine(Environment.CurrentDirectory, "tools");
            var presets= Path.Combine(Environment.CurrentDirectory, "author_presets");

            var path = new OutlineBuilderService(schema, presets)
                       .CreateOutlineAsync(proj, output).Result;

            return Ok($"Outline created: {path}");
        }

        // --------------------------------------------------------- premise / arc
        private static int RunExpand(string[] a, ILlmClient llm)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new PremiseExpanderService(llm).ExpandPremiseAsync(o, model).Wait();
            return Ok("Premise expanded.");
        }

        private static int RunArc(string[] a, ILlmClient llm)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new ArcDefinerService(llm).DefineArcAsync(o, model).Wait();
            return Ok("Story arc defined.");
        }

        // --------------------------------------------------------- characters
        private static int RunChars(string[] a, ILlmClient llm)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new CharactersOutlinerService(llm).DefineCharactersAsync(o, model).Wait();
            return Ok("Characters outlined.");
        }

        // --------------------------------------------------------- structure
        private static int RunStruct(string[] a, ILlmClient llm)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new StructureOutlinerService(llm).DefineStructureAsync(o, model).Wait();
            return Ok("Structure outlined.");
        }

        // -------------------------------------------------------------- helpers
        private static bool Try(string[] a, string flag, out string val)
        {
            val = "";
            for (var i = 0; i + 1 < a.Length; i++)
                if (a[i] == flag) { val = a[i + 1]; return true; }
            return false;
        }
        private static string Get(string[] a, string flag, string def) =>
            Try(a, flag, out var v) ? v : def;

        private static int Ok(string m)  { AnsiConsole.MarkupLine($"[green]{m}[/]"); return 0; }
        private static int Err(string m) { AnsiConsole.MarkupLine($"[red]{m}[/]");   return 1; }
        private static int Help()
        {
            AnsiConsole.MarkupLine("[bold]Novelist CLI[/]");
            AnsiConsole.MarkupLine(" outline create             --project <file> [--output <dir>]");
            AnsiConsole.MarkupLine(" outline expand-premise      --outline <file> [--model <id>] [--live]");
            AnsiConsole.MarkupLine(" outline define-arc          --outline <file> [--model <id>] [--live]");
            AnsiConsole.MarkupLine(" outline define-characters   --outline <file> [--model <id>] [--live]");
            AnsiConsole.MarkupLine(" outline define-structure    --outline <file> [--model <id>] [--live]");
            AnsiConsole.MarkupLine("Use --live (or -l) to invoke the real OpenAI API.");
            return 0;
        }

        // local stub keeps CLI functional without API calls
        private sealed class Stub : ILlmClient
        {
            public System.Threading.Tasks.Task<string> CompleteAsync(
                string p, string m, System.Threading.CancellationToken _ = default) =>
                System.Threading.Tasks.Task.FromResult("[]");
        }
    }
}
