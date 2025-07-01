using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Json.Schema;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;

namespace Novelist.OutlineBuilder;

/// <summary>
/// Creates a skeleton outline JSON file from a validated project JSON.
/// </summary>
public sealed class OutlineBuilderService
{
    private readonly string _schemaDirectory;
    private readonly string _authorPresetDirectory;

    public OutlineBuilderService(string schemaDirectory, string authorPresetDirectory)
    {
        _schemaDirectory       = schemaDirectory;
        _authorPresetDirectory = authorPresetDirectory;
    }

    /// <summary>
    /// Generates an outline, writes it to disk, and returns the path.
    /// </summary>
    public async Task<string> CreateOutlineAsync(
        string projectJsonPath,
        string? outputRoot,
        CancellationToken ct = default)
    {
        // ------------------------------------------------------------------
        // 1. Load JSON once; validate with JsonNode (System.Text.Json)
        // ------------------------------------------------------------------
        var jsonText = await File.ReadAllTextAsync(projectJsonPath, ct);

        var schemaPath    = Path.Combine(_schemaDirectory, "project.schema.v1.json");
        var projectSchema = JsonSchema.FromFile(schemaPath);

        var projectNode   = JsonNode.Parse(jsonText)!;
        var validation    = projectSchema.Evaluate(
            projectNode,
            new EvaluationOptions { OutputFormat = OutputFormat.Flag });

        if (!validation.IsValid)
            throw new InvalidOperationException("Invalid project JSON – schema validation failed.");

        // ------------------------------------------------------------------
        // 2. Work with JObject for easy mutation
        // ------------------------------------------------------------------
        var projectJson   = JObject.Parse(jsonText);

        var totalWords    = projectJson["totalWordCount"]!.Value<int>();
        var chapterCount  = projectJson["chapterCount"]!.Value<int>();
        var wordsPerChapter = totalWords / chapterCount;
        projectJson["wordsPerChapter"] = wordsPerChapter;

        // ------------------------------------------------------------------
        // 3. Build folder / filename structure
        // ------------------------------------------------------------------
        var workingTitle  = projectJson["workingTitle"]?.Value<string>() ?? string.Empty;
        var titleSlug     = SlugHelper.Slugify(workingTitle);
        var projectSlug   = SlugHelper.Slugify(workingTitle);
        var timestamp     = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        var projectFolder = Path.Combine(
            outputRoot ?? Directory.GetCurrentDirectory(),
            "projects",
            $"{projectSlug}_{timestamp}");

        var outlineFolder = Path.Combine(projectFolder, "outlines");
        Directory.CreateDirectory(outlineFolder);

        var outlineFileName = $"{titleSlug}_{timestamp}.json";
        var outlinePath     = Path.Combine(outlineFolder, outlineFileName);

        // ------------------------------------------------------------------
        // 4. Compose outline skeleton
        // ------------------------------------------------------------------
        var outline = new JObject
        {
            ["header"] = new JObject
            {
                ["schemaVersion"] = 1,
                ["dateCreated"]   = DateTime.UtcNow.ToString("o"),
                ["projectFile"]   = Path.GetRelativePath(projectFolder, projectJsonPath)
            },
            ["outlineProgress"] = "Init",
            ["premise"]         = projectJson["storyPremise"]!
        };

        // ---- famous author preset -------------------------------------------------
        outline["famousAuthor"] = null;
        var presetName = projectJson["famousAuthorPreset"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(presetName))
        {
            var presetPath = Path.Combine(_authorPresetDirectory, presetName);
            if (File.Exists(presetPath))
            {
                var presetJson = JObject.Parse(await File.ReadAllTextAsync(presetPath, ct));
                outline["famousAuthor"] = presetJson;
            }
        }

        // ---- empty character roster ----------------------------------------------
        outline["characters"] = new JArray();

        // ---- chapter placeholders -------------------------------------------------
        var chapters = new JArray();
        for (var i = 1; i <= chapterCount; i++)
        {
            chapters.Add(new JObject
            {
                ["number"]     = i,
                ["summary"]    = "",
                ["beats"]      = new JArray(),
                ["wordBudget"] = wordsPerChapter
            });
        }

        outline["chapters"] = chapters;

        // ------------------------------------------------------------------
        // 5. Persist to disk
        // ------------------------------------------------------------------
        await File.WriteAllTextAsync(outlinePath, outline.ToString(), ct);

        return outlinePath;
    }
}
