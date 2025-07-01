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
        // Arrange
        var baseDir   = AppContext.BaseDirectory;
        var schemaDir = baseDir;

        // unique temp root to avoid collisions with parallel tests
        var tempRoot  = Path.Combine(baseDir, $"outline-temp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var presets   = Path.Combine(tempRoot, "presets");
        Directory.CreateDirectory(presets);

        var projectPath = Path.Combine(baseDir, "thedoor.project.json");
        var service     = new OutlineBuilderService(schemaDir, presets);

        // Act
        var outlinePath = await service.CreateOutlineAsync(projectPath, tempRoot);

        // Assert
        File.Exists(outlinePath).Should().BeTrue();

        // Cleanup
        Directory.Delete(tempRoot, recursive: true);
    }
}
