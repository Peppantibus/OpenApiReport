using System.Text.Json;

namespace OpenApiReport.Cli;

public sealed class OpenApiReportConfig
{
    public string? Mode { get; init; }
    public string? Project { get; init; }
    public string? Configuration { get; init; }
    public string? Framework { get; init; }
    public string? SwaggerDoc { get; init; }
    public string? NswagConfig { get; init; }
    public string? Url { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Output { get; init; }
    public SnapshotDiffConfig? SnapshotDiff { get; init; }
}

public sealed class SnapshotDiffConfig
{
    public string? BaseRef { get; init; }
    public string? HeadRef { get; init; }
    public string? WorkDir { get; init; }
    public string? OutDir { get; init; }
    public string? ProjectName { get; init; }
    public List<string>? Formats { get; init; }
    public bool? FailOnBreaking { get; init; }
}

public static class OpenApiReportConfigLoader
{
    public static OpenApiReportConfig? LoadIfExists(string? configFilePath, string? baseDirectory)
    {
        var path = ResolveConfigPath(configFilePath, baseDirectory);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        var content = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OpenApiReportConfig>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static string? ResolveConfigPath(string? configFilePath, string? baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configFilePath))
        {
            return Path.GetFullPath(configFilePath);
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        return Path.Combine(baseDirectory, "openapi-report.json");
    }
}
