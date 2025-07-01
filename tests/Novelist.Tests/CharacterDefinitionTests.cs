using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Json.Schema;
using Novelist.OutlineBuilder;
using Xunit;

namespace Novelist.Tests;

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

        var expander = new PremiseExpanderService(new StubLlmClient());
        await expander.ExpandPremiseAsync(outline, "gpt-4o-mini");
        var definer  = new ArcDefinerService(new StubLlmClient());
        await definer.DefineArcAsync(outline, "gpt-4o-mini");

        var charService = new CharactersOutlinerService(new StubLlmClient());
        await charService.DefineCharactersAsync(outline, "gpt-4o-mini");

        var node   = JsonNode.Parse(File.ReadAllText(outline))!;
        var schema = JsonSchema.FromFile(Path.Combine(schemaDir, "outline.schema.v1.json"));
        schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Flag })
              .IsValid.Should().BeTrue();

        node["characters"]!.AsArray().Count.Should().BeGreaterThan(0);
        node["outlineProgress"]!.GetValue<string>()
            .Should().Be(OutlineProgress.CharactersOutlined.ToString());

        Directory.Delete(tempRoot, recursive: true);
    }
}
