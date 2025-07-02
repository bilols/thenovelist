using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Json.Schema;
using Novelist.OutlineBuilder;
using Xunit;

namespace Novelist.Tests
{
    public class StructureOutlineTests
    {
        [Fact]
        public async Task Structure_Is_Added_And_Schema_Passes()
        {
            var baseDir   = AppContext.BaseDirectory;
            var schemaDir = baseDir;

            var root = Path.Combine(baseDir, $"outline-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            var svc  = new OutlineBuilderService(schemaDir, Path.Combine(root, "p"));
            var proj = Path.Combine(baseDir, "thedoor.project.json");
            var file = await svc.CreateOutlineAsync(proj, root);

            var stub = new StubLlmClient();
            await new PremiseExpanderService(stub).ExpandPremiseAsync(file, "gpt-4o-mini");
            await new ArcDefinerService(stub).DefineArcAsync(file, "gpt-4o-mini");
            await new CharactersOutlinerService(stub).DefineCharactersAsync(file, "gpt-4o-mini");
            await new StructureOutlinerService(stub).DefineStructureAsync(file, "gpt-4o-mini");

            var node   = JsonNode.Parse(File.ReadAllText(file))!;
            var schema = JsonSchema.FromFile(
                Path.Combine(schemaDir, "outline.schema.v1.json"));

            schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Flag })
                  .IsValid.Should().BeTrue();

            node["chapters"]!.AsArray().Count.Should().BeGreaterThan(0);
            node["outlineProgress"]!.GetValue<string>()
                .Should().Be(OutlineProgress.StructureOutlined.ToString());

            Directory.Delete(root, true);
        }
    }
}
