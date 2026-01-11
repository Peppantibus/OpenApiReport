using OpenApiReport.Core;

namespace OpenApiReport.Cli;

public sealed class SnapshotDiffCommandHandler
{
    private readonly IGitClient _gitClient;
    private readonly IOpenApiSpecCapture _capture;

    public SnapshotDiffCommandHandler()
        : this(new ProcessGitClient(new ProcessRunner()), new OpenApiSpecCapture(new ProcessRunner()))
    {
    }

    public SnapshotDiffCommandHandler(IGitClient gitClient, IOpenApiSpecCapture capture)
    {
        _gitClient = gitClient;
        _capture = capture;
    }

    public int Execute(string[] args, TextWriter output, TextWriter errorOutput)
    {
        if (args.Length == 0 || !string.Equals(args[0], "snapshot-diff", StringComparison.OrdinalIgnoreCase))
        {
            errorOutput.WriteLine("Usage: openapi-report snapshot-diff --mode <swashbuckle|nswag|url> --base-ref <git-ref> --head-ref <git-ref> [options]");
            return 1;
        }

        try
        {
            var options = ParseOptions(args, errorOutput);
            if (options is null)
            {
                return 1;
            }

            return ExecuteSnapshot(options, output);
        }
        catch (Exception ex)
        {
            errorOutput.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private int ExecuteSnapshot(SnapshotDiffOptions options, TextWriter output)
    {
        var repoRoot = _gitClient.GetRepositoryRoot();
        var originalRef = _gitClient.GetCurrentRef();
        var workdir = options.WorkDir ?? Path.Combine(Path.GetTempPath(), "openapi-report", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workdir);

        var oldSpecPath = Path.Combine(workdir, "openapi.old.json");
        var newSpecPath = Path.Combine(workdir, "openapi.new.json");

        try
        {
            _gitClient.Checkout(options.BaseRef);
            _capture.Capture(options.CaptureOptions, oldSpecPath);

            _gitClient.Checkout(options.HeadRef);
            _capture.Capture(options.CaptureOptions, newSpecPath);
        }
        finally
        {
            _gitClient.Checkout(originalRef);
        }

        var outDir = options.OutDir ?? Path.Combine(repoRoot, "reports", "openapi");
        var reportDir = string.IsNullOrWhiteSpace(options.ProjectName)
            ? outDir
            : Path.Combine(outDir, options.ProjectName);
        Directory.CreateDirectory(reportDir);

        var oldSpec = OpenApiParser.ParseFile(oldSpecPath);
        var newSpec = OpenApiParser.ParseFile(newSpecPath);
        var diffEngine = new SemanticDiffEngine();
        var changes = diffEngine.Diff(oldSpec, newSpec);
        var summary = ReportFormatters.BuildSummary(changes);

        foreach (var format in options.Formats)
        {
            var content = format switch
            {
                "md" => ReportFormatters.FormatMarkdown(summary, changes),
                "markdown" => ReportFormatters.FormatMarkdown(summary, changes),
                "json" => ReportFormatters.FormatJson(summary, changes, oldSpecPath, newSpecPath, DateTimeOffset.UtcNow),
                "text" => ReportFormatters.FormatText(summary, changes),
                _ => throw new InvalidOperationException($"Unknown format '{format}'.")
            };

            var extension = format switch
            {
                "md" or "markdown" => "md",
                "json" => "json",
                "text" => "txt",
                _ => format
            };

            var filePath = Path.Combine(reportDir, $"openapi.diff.{extension}");
            File.WriteAllText(filePath, content);
        }

        output.WriteLine(ReportFormatters.FormatSummary(summary));
        if (options.FailOnBreaking && changes.Any(change => change.Severity == ChangeSeverity.Breaking))
        {
            return 2;
        }

        return 0;
    }

    private SnapshotDiffOptions? ParseOptions(string[] args, TextWriter errorOutput)
    {
        var baseRef = string.Empty;
        var headRef = string.Empty;
        string? workDir = null;
        string? outDir = null;
        string? projectName = null;
        var formats = new List<string> { "md", "json" };
        var failOnBreaking = false;

        var parseResult = CaptureArgumentParser.Parse(args, requireOutput: false, allowUnknownArguments: true, errorOutput);
        if (parseResult is null)
        {
            return null;
        }

        var captureOptions = parseResult.Value.Options;

        for (var index = 1; index < args.Length; index++)
        {
            var current = args[index];
            if (string.Equals(current, "--base-ref", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                baseRef = value;
                continue;
            }

            if (string.Equals(current, "--head-ref", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                headRef = value;
                continue;
            }

            if (string.Equals(current, "--workdir", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                workDir = value;
                continue;
            }

            if (string.Equals(current, "--out-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                outDir = value;
                continue;
            }

            if (string.Equals(current, "--project-name", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                projectName = value;
                continue;
            }

            if (string.Equals(current, "--formats", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                formats = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(format => format.ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                continue;
            }

            if (string.Equals(current, "--fail-on-breaking", StringComparison.OrdinalIgnoreCase))
            {
                failOnBreaking = true;
                continue;
            }
        }

        if (string.IsNullOrWhiteSpace(baseRef) || string.IsNullOrWhiteSpace(headRef))
        {
            errorOutput.WriteLine("Error: --base-ref and --head-ref are required.");
            return null;
        }

        if (!formats.Any())
        {
            formats.Add("md");
        }

        return new SnapshotDiffOptions
        {
            BaseRef = baseRef,
            HeadRef = headRef,
            WorkDir = workDir,
            OutDir = outDir ?? Path.Combine(_gitClient.GetRepositoryRoot(), "reports", "openapi"),
            Formats = formats,
            FailOnBreaking = failOnBreaking,
            CaptureOptions = captureOptions,
            ProjectName = projectName ?? new DirectoryInfo(_gitClient.GetRepositoryRoot()).Name
        };
    }

    private static bool TryReadValue(string[] args, ref int index, TextWriter errorOutput, out string value)
    {
        if (index + 1 >= args.Length)
        {
            errorOutput.WriteLine($"Error: {args[index]} requires a value.");
            value = string.Empty;
            return false;
        }

        value = args[index + 1];
        index++;
        return true;
    }
}

public sealed class SnapshotDiffOptions
{
    public required string BaseRef { get; init; }
    public required string HeadRef { get; init; }
    public string? WorkDir { get; init; }
    public string? OutDir { get; init; }
    public string? ProjectName { get; init; }
    public required List<string> Formats { get; init; }
    public bool FailOnBreaking { get; init; }
    public required CaptureOptions CaptureOptions { get; init; }
}
