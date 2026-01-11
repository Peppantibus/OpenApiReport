namespace OpenApiReport.Cli;

public enum CaptureMode
{
    Swashbuckle,
    Nswag,
    Url
}

public sealed class CaptureOptions
{
    public required CaptureMode Mode { get; init; }
    public string? ProjectPath { get; init; }
    public string Configuration { get; init; } = "Release";
    public string? Framework { get; init; }
    public string SwaggerDoc { get; init; } = "v1";
    public string? NswagConfigPath { get; init; }
    public string? Url { get; init; }
    public List<KeyValuePair<string, string>> Headers { get; init; } = new();
}
