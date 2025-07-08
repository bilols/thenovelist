using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Novelist.OutlineBuilder;
using Novelist.DraftBuilder;
using Spectre.Console;

namespace Novelist.Cli
{
    internal static class Program
    {
        private static bool _snapshot;

        private static int Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                         .AddUserSecrets(typeof(Program).Assembly, true)
                         .AddEnvironmentVariables()
                         .Build();

            bool live         = false;
            bool authorPreset = false;
            bool includeAud   = true;

            // ---------------- copy args into mutable list --------------------
            var list = new System.Collections.Generic.List<string>(args);

            // ---------------- strip global flags safely ----------------------
            for (int i = 0; i < list.Count; i++)
            {
                switch (list[i])
                {
                    case "--live":
                    case "-l":
                        live = true;
                        list.RemoveAt(i--);
                        break;

                    case "--snapshot":
                    case "-s":
                        _snapshot = true;
                        list.RemoveAt(i--);
                        break;

                    case "--author-preset":
                    case "-p":
                        authorPreset = true;
                        list.RemoveAt(i--);
                        break;

                    case "--no-audience":
                    case "-na":
                        includeAud = false;
                        list.RemoveAt(i--);
                        break;
                }
            }

            args = list.ToArray();

            ILlmClient? tmp = live ? CreateLiveClient(config)
                                   : new Stub();
            if (tmp is null)
                return 1;

            ILlmClient client = tmp;

            if (args.Length < 2)
                return Help();

            return (args[0].ToLowerInvariant(), args[1].ToLowerInvariant()) switch
            {
                ("outline", "create")                => RunCreate(args[2..]),
                ("outline", "expand-premise")        => RunExpand(args[2..], client, includeAud),
                ("outline", "define-arc")            => RunArc(args[2..], client, includeAud),
                ("outline", "define-characters")     => RunChars(args[2..], client, authorPreset, includeAud),
                ("outline", "define-subplots")       => RunSubPlots(args[2..], client, includeAud),
                ("outline", "expand-beats")          => RunBeats(args[2..], client, includeAud),
                ("outline", "define-structure")      => RunStruct(args[2..], client, includeAud),
                ("outline", "draft")                 => RunDraft(args[2..], client),
                ("outline", "mark-premise-expanded") => RunMark(args[2..]),
                _                                    => Help()
            };
        }

        // ---------------------------------------------------------------------
        //  Live-client helper
        // ---------------------------------------------------------------------
        private static ILlmClient? CreateLiveClient(IConfiguration cfg)
        {
            var key = cfg["OpenAI:Key"];
            if (string.IsNullOrWhiteSpace(key))
            {
                AnsiConsole.WriteLine("ERROR: OpenAI key not set.");
                return null;
            }

            Environment.SetEnvironmentVariable("OPENAI_KEY", key);

            var baseUrl = cfg["OpenAI:Base"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                Environment.SetEnvironmentVariable("OPENAI_BASE", baseUrl);

            AnsiConsole.WriteLine("Using live OpenAI client.");
            return new OpenAiLlmClient();
        }

        // ---------------------------------------------------------------------
        //  Individual command runners
        // ---------------------------------------------------------------------
        private static int RunCreate(string[] a)
        {
            if (!Try(a, "--project", out var proj))
                return Err("--project required");
            Try(a, "--output", out var outDir);

            var schemaDir = Path.Combine(Environment.CurrentDirectory, "tools");
            var presetDir = Path.Combine(Environment.CurrentDirectory, "author_presets");

            var path = new OutlineBuilderService(schemaDir, presetDir)
                      .CreateOutlineAsync(proj, outDir).Result;

            SaveSnapshot(path);
            return Ok($"Outline created: {path}");
        }

        private static int RunExpand(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");

            var model = Get(a, "--model", "gpt-4o-mini");

            int maxWords = 250;
            if (Try(a, "--premise-words", out var wStr))
            {
                if (!int.TryParse(wStr, out maxWords) || maxWords is < 150 or > 350)
                    return Err("--premise-words must be 150-350");
            }

            new PremiseExpanderService(llm, aud)
               .ExpandPremiseAsync(o, model, maxWords).Wait();

            SaveSnapshot(o);
            return Ok("Premise expanded.");
        }

        private static int RunArc(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new ArcDefinerService(llm, aud)
               .DefineArcAsync(o, model).Wait();

            SaveSnapshot(o);
            return Ok("Story arc defined.");
        }

        private static int RunChars(string[] a, ILlmClient llm, bool preset, bool aud)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new CharactersOutlinerService(llm, preset)
               .DefineCharactersAsync(o, model).Wait();

            SaveSnapshot(o);
            return Ok("Characters outlined.");
        }

        private static int RunSubPlots(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new SubPlotDefinerService(llm, aud)
               .DefineSubPlotsAsync(o, model).Wait();

            SaveSnapshot(o);
            return Ok("Sub-plots defined.");
        }

        private static int RunBeats(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new BeatsExpanderService(llm, aud)
               .ExpandBeatsAsync(o, model).Wait();

            SaveSnapshot(o);
            return Ok("Beats expanded.");
        }

        private static int RunStruct(string[] a, ILlmClient llm, bool aud)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");
            var model = Get(a, "--model", "gpt-4o-mini");

            new StructureOutlinerService(llm, aud)
               .DefineStructureAsync(o, model).Wait();

            SaveSnapshot(o);
            return Ok("Structure outlined.");
        }

        private static int RunDraft(string[] a, ILlmClient llm)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");
            if (!Try(a, "--output", out var outDir))
                return Err("--output required");

            var model = Get(a, "--model", "gpt-4o-mini");

            int start = 1, end = int.MaxValue;
            if (Try(a, "--chapters", out var r))
            {
                var parts = r.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out start) &&
                    int.TryParse(parts[1], out end) &&
                    start > 0 && end >= start)
                { /* ok */ }
                else
                    return Err("--chapters format must be start-end (e.g., 5-12)");
            }

            new DraftBuilderService(llm)
               .BuildDraftAsync(o, outDir, model, start, end).Wait();

            return Ok("Draft build completed.");
        }

        private static int RunMark(string[] a)
        {
            if (!Try(a, "--outline", out var o))
                return Err("--outline required");

            PremiseMarkerService.MarkPremiseExpanded(o);
            SaveSnapshot(o);
            return Ok("Outline marked as PremiseExpanded.");
        }

        // ---------------------------------------------------------------------
        //  Utility helpers
        // ---------------------------------------------------------------------
        private static void SaveSnapshot(string path)
        {
            if (!_snapshot)
                return;

            try
            {
                var j     = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                var phase = j["outlineProgress"]?.ToString() ?? "Unknown";

                var dir  = Path.GetDirectoryName(path)!;
                var name = Path.GetFileNameWithoutExtension(path);
                var dest = Path.Combine(dir, $"{name}_{phase}.json");

                File.Copy(path, dest, true);
                AnsiConsole.WriteLine($"Snapshot saved: {dest}");
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine($"Warning: snapshot failed – {ex.Message}");
            }
        }

        private static bool Try(string[] a, string f, out string v)
        {
            v = string.Empty;
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (a[i] == f)
                {
                    v = a[i + 1];
                    return true;
                }
            }
            return false;
        }

        private static string Get(string[] a, string f, string d)
            => Try(a, f, out var v) ? v : d;

        private static int Ok(string m)
        {
            AnsiConsole.WriteLine(m);
            return 0;
        }

        private static int Err(string m)
        {
            AnsiConsole.WriteLine($"ERROR: {m}");
            return 1;
        }

        private static int Help()
        {
            AnsiConsole.WriteLine("Novelist CLI");
            AnsiConsole.WriteLine(" outline create --project <file> [--output <dir>] [-s]");
            AnsiConsole.WriteLine(" outline expand-premise --outline <file> [--premise-words <150-350>] [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.WriteLine(" outline define-arc --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.WriteLine(" outline define-characters --outline <file> [--model <id>] [-l] [-s] [-p] [-na]");
            AnsiConsole.WriteLine(" outline define-subplots --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.WriteLine(" outline expand-beats --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.WriteLine(" outline define-structure --outline <file> [--model <id>] [-l] [-s] [-na]");
            AnsiConsole.WriteLine(" outline draft --outline <file> --output <dir> [--chapters <start-end>] [--model <id>] [-l] [-s]");
            AnsiConsole.WriteLine(" outline mark-premise-expanded --outline <file> [-s]");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Flags: -l live | -s snapshot | -p author-preset | -na no-audience");
            return 0;
        }

        private sealed class Stub : ILlmClient
        {
            public System.Threading.Tasks.Task<string> CompleteAsync(
                string p, string m, System.Threading.CancellationToken _ = default)
                => System.Threading.Tasks.Task.FromResult("[]");
        }
    }
}
