using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Json.Schema;
using Novelist.OutlineBuilder;
using Xunit;

namespace Novelist.Tests;

public class ArcDefinitionTests
{
    [Fact]
    public async Task Arc_Is_Defined_And_Schema_Validates()
    {
        // Arrange
        var baseDir   = AppContext.BaseDirectory;
        var schemaDir = baseDir;

        var tempRoot = Path.Combine(baseDir, $"outline-temp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var builder = new OutlineBuilderService(schemaDir, Path.Combine(tempRoot, "presets"));

        var projectPath = Path.Combine(baseDir, "thedoor.project.json");
        var outlinePath = await builder.CreateOutlineAsync(projectPath, tempRoot);

        var expander = new PremiseExpanderService(new StubLlmClient());
        await expander.ExpandPremiseAsync(outlinePath, "gpt-4o-mini");

        var definer  = new ArcDefinerService(new StubLlmClient());
        await definer.DefineArcAsync(outlinePath, "gpt-4o-mini");

        // Assert – validate against outline schema
        var outlineNode   = JsonNode.Parse(File.ReadAllText(outlinePath))!;
        var outlineSchema = JsonSchema.FromFile(Path.Combine(schemaDir, "outline.schema.v1.json"));

        var result = outlineSchema.Evaluate(outlineNode, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        result.IsValid.Should().BeTrue();

        outlineNode!["outlineProgress"]!.GetValue<string>()
                   .Should().Be(OutlineProgress.ArcDefined.ToString());

        Directory.Delete(tempRoot, recursive: true);
    }
}
