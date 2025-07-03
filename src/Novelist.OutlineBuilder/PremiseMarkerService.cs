using System.IO;
using Newtonsoft.Json.Linq;

namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Marks an outline as PremiseExpanded when the premise is already full length.
    /// </summary>
    public static class PremiseMarkerService
    {
        public static void MarkPremiseExpanded(string outlinePath)
        {
            var outline = JObject.Parse(File.ReadAllText(outlinePath));

            outline["outlineProgress"] = OutlineProgress.PremiseExpanded.ToString();
            outline["header"]!["schemaVersion"] =
                outline["header"]!["schemaVersion"]!.Value<int>() + 1;

            File.WriteAllText(outlinePath, outline.ToString());
        }
    }
}
