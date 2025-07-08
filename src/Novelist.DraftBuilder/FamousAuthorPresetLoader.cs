using System.IO;
using Newtonsoft.Json.Linq;

namespace Novelist.DraftBuilder
{
    internal static class FamousAuthorPresetLoader
    {
        public static DraftStyleOptions Load(string presetDir, string presetFile)
        {
            string path = Path.Combine(presetDir, presetFile);
            var j      = JObject.Parse(File.ReadAllText(path));

            return new DraftStyleOptions(
                Voice:             j["voice"]?.ToString()           ?? "Neutral",
                LexicalDensity:    j["lexical_density"]?.Value<double>() ?? 0.45,
                SentenceLength:    j["sentence_length"]?.Value<int>()    ?? 15,
                Hallmarks:         string.Join("; ", j["hallmarks"] ?? new JArray()),
                PreferredTone:     j["preferred_tone"]?.ToString()   ?? "Neutral",
                ForbiddenElements: j["formatting_rules"]?["forbidden_elements"]?.ToString() ?? "",
                AuthorName:        Path.GetFileNameWithoutExtension(presetFile));
        }
    }
}
