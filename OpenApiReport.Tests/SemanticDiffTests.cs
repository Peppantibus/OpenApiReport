using OpenApiReport.Cli;
using OpenApiReport.Core;
using System.Text.Json;

namespace OpenApiReport.Tests;

public class SemanticDiffTests
{
    [Fact]
    public void ParameterRequiredRelaxed_IsDetectedAsNonBreaking()
    {
        var oldSpec = CreateSpecWithParameter(required: true);
        var newSpec = CreateSpecWithParameter(required: false);

        var changes = DiffSpecs(oldSpec, newSpec);
        var change = Assert.Single(changes);

        Assert.Equal("Parameter requirement relaxed", change.Title);
        Assert.Equal("paths./orders.get.parameters[id].required", change.Pointer);
        Assert.Equal(ChangeSeverity.Additive, change.Severity);
    }

    [Fact]
    public void ParameterRemoved_IsBreaking()
    {
        var oldSpec = CreateSpecWithParameter(required: false);
        var newSpec = CreateSpecWithoutParameter();

        var changes = DiffSpecs(oldSpec, newSpec);

        Assert.Contains(changes, change => change.Title == "Parameter removed" && change.Severity == ChangeSeverity.Breaking);
    }

    [Fact]
    public void OperationRemoved_IsBreaking()
    {
        var oldSpec = CreateSpecWithOperation("get");
        var newSpec = CreateSpecWithoutOperation();

        var changes = DiffSpecs(oldSpec, newSpec);

        Assert.Contains(changes, change => change.Title == "Operation removed" && change.Severity == ChangeSeverity.Breaking);
    }

    [Fact]
    public void OperationAdded_IsAdditive()
    {
        var oldSpec = CreateSpecWithoutOperation();
        var newSpec = CreateSpecWithOperation("get");

        var changes = DiffSpecs(oldSpec, newSpec);

        Assert.Contains(changes, change => change.Title == "Operation added" && change.Severity == ChangeSeverity.Additive);
    }

    [Fact]
    public void EnumNarrowed_IsBreaking_EnumExpanded_IsAdditive()
    {
        var oldSpec = CreateSpecWithEnum(new[] { "A", "B" });
        var newSpec = CreateSpecWithEnum(new[] { "A" });
        var breakingChanges = DiffSpecs(oldSpec, newSpec);

        Assert.Contains(breakingChanges, change => change.Title == "Enum values removed" && change.Severity == ChangeSeverity.Breaking);

        var expandedChanges = DiffSpecs(newSpec, oldSpec);
        Assert.Contains(expandedChanges, change => change.Title == "Enum values added" && change.Severity == ChangeSeverity.Additive);
    }

    [Fact]
    public void MarkdownReport_IncludesGroupingAndMeaning()
    {
        var oldSpec = CreateSpecWithOperation("get");
        var newSpec = CreateSpecWithoutOperation();

        var changes = DiffSpecs(oldSpec, newSpec);
        var summary = ReportFormatters.BuildSummary(changes);
        var markdown = ReportFormatters.FormatMarkdown(summary, changes);

        Assert.Contains("## Breaking", markdown);
        Assert.Contains("### Tag:", markdown);
        Assert.Contains("#### GET /orders", markdown);
        Assert.Contains("Meaning:", markdown);
        Assert.Contains("SuggestedAction:", markdown);
    }

    [Fact]
    public void ExitCode_IsTwoWhenBreakingChangesExist()
    {
        var oldSpec = CreateSpecWithOperation("get");
        var newSpec = CreateSpecWithoutOperation();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var oldPath = WriteSpec(tempDir, "old.json", oldSpec);
        var newPath = WriteSpec(tempDir, "new.json", newSpec);

        var handler = new DiffCommandHandler();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = handler.Execute(new[] { "diff", oldPath, newPath, "--format", "text" }, output, error);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void ExitCode_IsZeroWhenNoBreakingChanges()
    {
        var oldSpec = CreateSpecWithoutOperation();
        var newSpec = CreateSpecWithoutOperation();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var oldPath = WriteSpec(tempDir, "old.json", oldSpec);
        var newPath = WriteSpec(tempDir, "new.json", newSpec);

        var handler = new DiffCommandHandler();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = handler.Execute(new[] { "diff", oldPath, newPath, "--format", "text" }, output, error);

        Assert.Equal(0, exitCode);
    }

    private static IReadOnlyList<ChangeRecord> DiffSpecs(string oldSpecJson, string newSpecJson)
    {
        using var oldDoc = JsonDocument.Parse(oldSpecJson);
        using var newDoc = JsonDocument.Parse(newSpecJson);
        var oldSpec = OpenApiParser.Parse(oldDoc.RootElement);
        var newSpec = OpenApiParser.Parse(newDoc.RootElement);
        var diffEngine = new SemanticDiffEngine();
        return diffEngine.Diff(oldSpec, newSpec);
    }

    private static string CreateSpecWithParameter(bool required)
    {
        return $$"""
        {
          "openapi": "3.0.0",
          "paths": {
            "/orders": {
              "get": {
                "tags": ["orders"],
                "parameters": [
                  {
                    "name": "id",
                    "in": "query",
                    "required": {{required.ToString().ToLowerInvariant()}},
                    "schema": { "type": "string" }
                  }
                ],
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;
    }

    private static string CreateSpecWithoutParameter()
    {
        return """
        {
          "openapi": "3.0.0",
          "paths": {
            "/orders": {
              "get": {
                "tags": ["orders"],
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;
    }

    private static string CreateSpecWithOperation(string method)
    {
        return $$"""
        {
          "openapi": "3.0.0",
          "paths": {
            "/orders": {
              "{{method}}": {
                "tags": ["orders"],
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;
    }

    private static string CreateSpecWithoutOperation()
    {
        return """
        {
          "openapi": "3.0.0",
          "paths": { }
        }
        """;
    }

    private static string CreateSpecWithEnum(IEnumerable<string> values)
    {
        var enumValues = string.Join(", ", values.Select(value => $"\"{value}\""));
        return $$"""
        {
          "openapi": "3.0.0",
          "components": {
            "schemas": {
              "OrderStatus": {
                "type": "string",
                "enum": [ {{enumValues}} ]
              }
            }
          }
        }
        """;
    }

    private static string WriteSpec(string directory, string fileName, string contents)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, contents);
        return path;
    }
}
