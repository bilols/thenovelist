using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Novelist.DraftBuilder
{
    internal static class FamousAuthorPresetLoader
    {
        public static DraftStyleOptions Load(string presetDir, string presetFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(presetFile))
                    return Neutral();

                var path = Path.Combine(presetDir, presetFile);
                if (!File.Exists(path))
                    return Neutral();

                var j = JObject.Parse(File.ReadAllText(path));

                return new DraftStyleOptions(
                    j["voice"]?.ToString() ?? "Neutral",
                    j["lexical_density"]?.Value<double>() ?? 0.45,
                    j["sentence_length"]?.Value<int>() ?? 15,
                    string.Join("; ", j["hallmarks"] ?? new JArray()),
                    j["preferred_tone"]?.ToString() ?? "Neutral",
                    j["formatting_rules"]?["forbidden_elements"]?.ToString() ?? "",
                    Path.GetFileNameWithoutExtension(presetFile));
            }
            catch (Exception)
            {
                // any IO or JSON issue → fall back to neutral
                return Neutral();
            }
        }

        private static DraftStyleOptions Neutral() =>
            new("Neutral", 0.45, 15, "", "Neutral", "", "Unknown");
    }
}
