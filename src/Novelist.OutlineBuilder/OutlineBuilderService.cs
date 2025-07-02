using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Seeds a blank outline file from a project.json definition.
    /// </summary>
    public sealed class OutlineBuilderService
    {
        private readonly string _schemaDir;
        private readonly string _presetDir;

        public OutlineBuilderService(string schemaDir, string presetDir)
        {
            _schemaDir = schemaDir;
            _presetDir = presetDir;
        }

        /// <summary>
        /// Creates a new outline JSON file on disk and returns its full path.
        /// </summary>
        public async Task<string> CreateOutlineAsync(
            string projectFile,
            string? outputDir = null,
            CancellationToken ct = default)
        {
            var projectJson =
                JObject.Parse(await File.ReadAllTextAsync(projectFile, ct));

            // Mandatory fields for schema v1
            var totalWords   = projectJson["totalWordCount"]!.Value<int>();
            var chapterCount = projectJson["chapterCount"]!.Value<int>();

            var outline = new JObject
            {
                ["header"] = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["dateCreated"]   = DateTime.UtcNow.ToString("O"),
                    ["projectFile"]   = Path.GetRelativePath(
                                            outputDir ?? Path.GetDirectoryName(projectFile)!,
                                            projectFile)
                },

                ["outlineProgress"] = OutlineProgress.Init.ToString(),
                ["premise"]         = string.Empty,
                ["totalWordCount"]  = totalWords,
                ["chapterCount"]    = chapterCount
            };

            // Persist
            outputDir ??= Path.Combine(
                Path.GetDirectoryName(projectFile)!, "outlines");

            Directory.CreateDirectory(outputDir);

            var fileName = $"outline_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var fullPath = Path.Combine(outputDir, fileName);

            await File.WriteAllTextAsync(fullPath, outline.ToString(), ct);
            return fullPath;
        }
    }
}
