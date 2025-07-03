using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Novelist.OutlineBuilder;
using Spectre.Console;

namespace Novelist.Cli
{
    internal static class Program
    {
        private static bool _snapshot;

        private static int Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets(typeof(Program).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

            bool live         = false;
            bool authorPreset = false;
            bool includeAud   = true;

            var list = new System.Collections.Generic.List<string>(args);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                switch (list[i])
                {
                    case "--live":
                    case "-l":  live = true;  list.RemoveAt(i); break;
                    case "--snapshot":
                    case "-s":  _snapshot = true; list.RemoveAt(i); break;
                    case "--author-preset":
                    case "-p":  authorPreset = true; list.RemoveAt(i); break;
                    case "--no-audience":
                    case "-na": includeAud = false; list.RemoveAt(i); break;
                }
            }
            args = list.ToArray();

            // ----------------------------------------------------------------
            // LLM client (fixes nullable warning)
            // ----------------------------------------------------------------
            ILlmClient? tmp = live ? CreateLiveClient(config) : new Stub();
            if (tmp is null) return 1;             // live client creation failed
            ILlmClient client = tmp;               // now non‑nullable

            if (args.Length < 2) return Help();

            return (args[0].ToLowerInvariant(), args[1].ToLowerInvariant()) switch
            {
                ("outline", "create")               => RunCreate(args[2..]),
                ("outline", "expand-premise")       => RunExpand(args[2..], client, includeAud),
                ("outline", "define-arc")           => RunArc(args[2..], client, includeAud),
                ("outline", "define-characters")    => RunChars(args[2..], client, authorPreset, includeAud),
                ("outline", "define-structure")     => RunStruct(args[2..], client, includeAud),
                ("outline", "mark-premise-expanded")=> RunMark(args[2..]),
                _                                   => Help()
            };
        }

        // ---------------------------------------------------------------- live client
        private static ILlmClient? CreateLiveClient(IConfiguration cfg)
        {
            var key = cfg["OpenAI:Key"];
            if (string.IsNullOrWhiteSpace(key))
            {
                AnsiConsole.MarkupLine("[red]ERROR: OpenAI key not set.[/]");
                return null;
            }
            Environment.SetEnvironmentVariable("OPENAI_KEY", key);
            var baseUrl = cfg["OpenAI:Base"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                Environment.SetEnvironmentVariable("OPENAI_BASE", baseUrl);

            AnsiConsole.MarkupLine("[green]Using live OpenAI client.[/]");
            return new OpenAiLlmClient();
        }

        // ---------------------------------------------------------------- create
        private static int RunCreate(string[] a)
        {
            if (!Try(a, "--project", out var proj)) return Err("--project required");
            Try(a, "--output", out var output);

            var schemaDir = Path.Combine(Environment.CurrentDirectory, "tools");
            var presetDir = Path.Combine(Environment.CurrentDirectory, "author_presets");

            var path = new OutlineBuilderService(schemaDir, presetDir)
                       .CreateOutlineAsync(proj, output).Result;

            SaveSnapshot(path);
            return Ok($"Outline created: {path}");
        }

        // ---------------------------------------------------------------- premise
        private static int RunExpand(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new PremiseExpanderService(llm, aud).ExpandPremiseAsync(o, model).Wait();
            SaveSnapshot(o);
            return Ok("Premise expanded.");
        }

        // ---------------------------------------------------------------- arc
        private static int RunArc(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new ArcDefinerService(llm, aud).DefineArcAsync(o, model).Wait();
            SaveSnapshot(o);
            return Ok("Story arc defined.");
        }

        // ---------------------------------------------------------------- characters
        private static int RunChars(string[] a, ILlmClient llm, bool preset, bool aud)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new CharactersOutlinerService(llm, preset)
                .DefineCharactersAsync(o, model).Wait();

            SaveSnapshot(o);
            return Ok("Characters outlined.");
        }

        // ---------------------------------------------------------------- structure
        private static int RunStruct(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new StructureOutlinerService(llm, aud).DefineStructureAsync(o, model).Wait();
            SaveSnapshot(o);
            return Ok("Structure outlined.");
        }

        // ---------------------------------------------------------------- mark premise
        private static int RunMark(string[] a)
        {
            if (!Try(a, "--outline", out var o)) return Err("--outline required");
            PremiseMarkerService.MarkPremiseExpanded(o);
            SaveSnapshot(o);
            return Ok("Outline marked as PremiseExpanded.");
        }

        // ---------------------------------------------------------------- snapshot
        private static void SaveSnapshot(string path)
        {
            if (!_snapshot) return;
            try
            {
                var j     = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                var phase = j["outlineProgress"]?.ToString() ?? "Unknown";
                var dir   = Path.GetDirectoryName(path)!;
                var name  = Path.GetFileNameWithoutExtension(path);
                var dest  = Path.Combine(dir, $"{name}_{phase}.json");
                File.Copy(path, dest, true);
                AnsiConsole.MarkupLine($"[grey]Snapshot saved: {dest}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: snapshot failed – {ex.Message}[/]");
            }
        }

        // ---------------------------------------------------------------- helpers
        private static bool Try(string[] a, string flag, out string val)
        {
            val = "";
            for (int i = 0; i + 1 < a.Length; i++)
                if (a[i] == flag) { val = a[i + 1]; return true; }
            return false;
        }
        private static string Get(string[] a, string flag, string def) =>
            Try(a, flag, out var v) ? v : def;

        private static int Ok (string m) { AnsiConsole.MarkupLine($"[green]{m}[/]"); return 0; }
        private static int Err(string m) { AnsiConsole.MarkupLine($"[red]{m}[/]");   return 1; }

        private static int Help()
        {
            AnsiConsole.MarkupLine("[bold]Novelist CLI[/]");
            AnsiConsole.MarkupLine(" outline create               --project <file> [--output <dir>] [-s]");
            AnsiConsole.MarkupLine(" outline expand-premise        --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.MarkupLine(" outline define-arc            --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.MarkupLine(" outline define-characters     --outline <file> [--model <id>] [-l] [-s] [-p] [-na]");
            AnsiConsole.MarkupLine(" outline define-structure      --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.MarkupLine(" outline mark-premise-expanded --outline <file> [-s]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Flags:");
            AnsiConsole.MarkupLine("  -l,  --live            Use live OpenAI completions");
            AnsiConsole.MarkupLine("  -s,  --snapshot        Save snapshot after each step");
            AnsiConsole.MarkupLine("  -p,  --author-preset   Include famousAuthorPreset during character pass");
            AnsiConsole.MarkupLine("  -na, --no-audience     Suppress targetAudience cue in prompts");
            return 0;
        }

        // ---------------------------------------------------------------- stub
        private sealed class Stub : ILlmClient
        {
            public System.Threading.Tasks.Task<string> CompleteAsync(
                string p, string m, System.Threading.CancellationToken _ = default) =>
                System.Threading.Tasks.Task.FromResult("[]");
        }
    }
}
