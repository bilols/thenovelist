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
    public class CharacterDefinitionTests
    {
        [Fact]
        public async Task Characters_Are_Added_And_Schema_Passes()
        {
            var baseDir   = AppContext.BaseDirectory;
            var schemaDir = baseDir;

            var tempRoot  = Path.Combine(baseDir, $"outline-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var builder = new OutlineBuilderService(schemaDir, Path.Combine(tempRoot, "p"));
            var project = Path.Combine(baseDir, "thedoor.project.json");
            var outline = await builder.CreateOutlineAsync(project, tempRoot);

            var stub = new StubLlmClient();
            await new PremiseExpanderService(stub).ExpandPremiseAsync(outline, "gpt-4o-mini");
            await new ArcDefinerService(stub).DefineArcAsync(outline, "gpt-4o-mini");
            await new CharactersOutlinerService(stub).DefineCharactersAsync(outline, "gpt-4o-mini");

            //-----------------------------------------------------------------
            // Schema validation
            //-----------------------------------------------------------------
            var node   = JsonNode.Parse(File.ReadAllText(outline))!;
            var schema = JsonSchema.FromFile(Path.Combine(schemaDir, "outline.schema.v1.json"));
            schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Flag })
                  .IsValid.Should().BeTrue();

            //-----------------------------------------------------------------
            // Roster assertions
            //-----------------------------------------------------------------
            var chars = node["characters"]!.AsArray();
            chars.Count.Should().Be(5); // 1 protagonist + 2 supporting + 2 minor

            var role = chars[0]?["role"]?.GetValue<string>();
            role.Should().NotBeNullOrEmpty();
            role!.Should().StartWith("Protagonist:");

            node["outlineProgress"]!.GetValue<string>()
                .Should().Be(OutlineProgress.CharactersOutlined.ToString());

            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
