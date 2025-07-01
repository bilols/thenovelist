using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Novelist.OutlineBuilder;
using Xunit;

namespace Novelist.Tests;

public class PremiseExpansionTests
{
    [Fact]
    public async Task Premise_Is_Expanded_And_Progress_Advanced()
    {
        // Arrange
        var baseDir   = AppContext.BaseDirectory;
        var schemaDir = baseDir;

        var tempRoot  = Path.Combine(baseDir, $"outline-temp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var presets   = Path.Combine(tempRoot, "presets");
        Directory.CreateDirectory(presets);

        var projectPath = Path.Combine(baseDir, "thedoor.project.json");
        var builder     = new OutlineBuilderService(schemaDir, presets);
        var outlinePath = await builder.CreateOutlineAsync(projectPath, tempRoot);

        var expander = new PremiseExpanderService(new StubLlmClient());

        // Act
        await expander.ExpandPremiseAsync(outlinePath, "gpt-4o-mini");

        // Assert
        var outlineJson = JObject.Parse(File.ReadAllText(outlinePath));
        outlineJson["outlineProgress"]!.Value<string>()
                  .Should().Be(OutlineProgress.PremiseExpanded.ToString());
        outlineJson["premise"]!.Value<string>()
                  .Should().Contain("StubLlmClient");

        // Cleanup
        Directory.Delete(tempRoot, recursive: true);
    }
}
