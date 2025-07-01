using System.IO;
using System.Text.Json.Nodes;
using FluentAssertions;
using Json.Schema;
using Xunit;

namespace Novelist.Tests;

public class ProjectSchemaValidationTests
{
    [Fact]
    public void TheDoorProject_Should_Pass_ProjectSchemaV1()
    {
        // Arrange – locate schema and sample file relative to the test DLL directory.
        var baseDir   = AppContext.BaseDirectory;
        var schemaPath  = Path.Combine(baseDir, "project.schema.v1.json");
        var projectPath = Path.Combine(baseDir, "thedoor.project.json");

        File.Exists(schemaPath).Should().BeTrue("project schema must be copied to test output");
        File.Exists(projectPath).Should().BeTrue("sample project file must be copied to test output");

        var schema  = JsonSchema.FromFile(schemaPath);
        var sample  = JsonNode.Parse(File.ReadAllText(projectPath))!;

        // Act
        var result = schema.Evaluate(sample, new EvaluationOptions { OutputFormat = OutputFormat.Flag });

        // Assert
        result.IsValid.Should().BeTrue("sample project should conform to project.schema.v1.json");
    }
}
