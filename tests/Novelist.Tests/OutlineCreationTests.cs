using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Novelist.OutlineBuilder;
using Xunit;

namespace Novelist.Tests;

public class OutlineCreationTests
{
    [Fact]
    public async Task Outline_Is_Created_And_File_Exists()
    {
        // Test output directory
        var baseDir   = AppContext.BaseDirectory;

        var schemaDir = Path.Combine(baseDir);           // schema copied here
        var presetDir = Path.Combine(baseDir, "presets"); // optional presets; ensure exists
        Directory.CreateDirectory(presetDir);

        var projectPath = Path.Combine(baseDir, "thedoor.project.json");

        var service = new OutlineBuilderService(schemaDir, presetDir);

        var tempRoot   = Path.Combine(baseDir, "outline-temp");
        var outlinePath = await service.CreateOutlineAsync(projectPath, tempRoot);

        File.Exists(outlinePath).Should().BeTrue("the outline file must be written to disk");

        // Clean up temp folder
        Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(outlinePath))!, recursive: true);
    }
}
