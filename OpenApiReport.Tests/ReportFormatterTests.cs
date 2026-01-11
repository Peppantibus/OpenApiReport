using OpenApiReport.Core;
using System.Text.Json;

namespace OpenApiReport.Tests;

public class ReportFormatterTests
{
    [Fact]
    public void JsonReport_IncludesStableSchemaFields()
    {
        var changes = new List<ChangeRecord>
        {
            new()
            {
                Severity = ChangeSeverity.Breaking,
                RiskScore = 10,
                Tag = "orders",
                Endpoint = "GET /orders",
                Pointer = "paths./orders.get",
                Title = "Operation removed",
                Before = "present",
                After = "missing",
                Meaning = "Removed op",
                SuggestedAction = "Re-add"
            },
            new()
            {
                Severity = ChangeSeverity.Additive,
                RiskScore = 1,
                Tag = "orders",
                Endpoint = "POST /orders",
                Pointer = "paths./orders.post",
                Title = "Operation added",
                Before = "missing",
                After = "present",
                Meaning = "Added op",
                SuggestedAction = "Update"
            }
        };

        var summary = ReportFormatters.BuildSummary(changes);
        var json = ReportFormatters.FormatJson(summary, changes, "old.json", "new.json", DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("generatedAtUtc", out var generatedAt));
        Assert.Equal(JsonValueKind.String, generatedAt.ValueKind);
        Assert.Equal("old.json", root.GetProperty("oldSpecPath").GetString());
        Assert.Equal("new.json", root.GetProperty("newSpecPath").GetString());

        var summaryElement = root.GetProperty("summary");
        Assert.Equal(1, summaryElement.GetProperty("breaking").GetInt32());
        Assert.Equal(0, summaryElement.GetProperty("risky").GetInt32());
        Assert.Equal(1, summaryElement.GetProperty("additive").GetInt32());
        Assert.Equal(0, summaryElement.GetProperty("cosmetic").GetInt32());
        Assert.Equal(2, summaryElement.GetProperty("total").GetInt32());

        var topChanges = root.GetProperty("topChanges");
        Assert.Equal(JsonValueKind.Array, topChanges.ValueKind);
        Assert.Equal("Breaking", topChanges[0].GetProperty("category").GetString());

        var changeList = root.GetProperty("changes");
        Assert.Equal(JsonValueKind.Array, changeList.ValueKind);
        Assert.Equal("Breaking", changeList[0].GetProperty("category").GetString());
        Assert.Equal("orders", changeList[0].GetProperty("tag").GetString());
        Assert.Equal("paths./orders.get", changeList[0].GetProperty("pointer").GetString());
        Assert.True(changeList[0].TryGetProperty("meaning", out _));
        Assert.True(changeList[0].TryGetProperty("suggestedAction", out _));
        Assert.True(changeList[0].TryGetProperty("riskScore", out _));
    }

    [Fact]
    public void MarkdownReport_MatchesGoldenFixture()
    {
        var changes = new List<ChangeRecord>
        {
            new()
            {
                Severity = ChangeSeverity.Breaking,
                RiskScore = 10,
                Tag = "orders",
                Endpoint = "GET /orders",
                Pointer = "paths./orders.get",
                Title = "Operation removed",
                Before = "present",
                After = "missing",
                Meaning = "Removed op",
                SuggestedAction = "Re-add"
            },
            new()
            {
                Severity = ChangeSeverity.Additive,
                RiskScore = 2,
                Tag = "orders",
                Endpoint = "POST /orders",
                Pointer = "paths./orders.post",
                Title = "Operation added",
                Before = "missing",
                After = "present",
                Meaning = "Added op",
                SuggestedAction = "Update"
            }
        };

        var summary = ReportFormatters.BuildSummary(changes);
        var markdown = ReportFormatters.FormatMarkdown(summary, changes);
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "markdown-report.md"));

        Assert.Equal(expected, markdown);
    }
}
