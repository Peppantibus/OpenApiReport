using OpenApiReport.Core;

namespace OpenApiReport.Cli;

public sealed class DiffCommandHandler
{
    public int Execute(string[] args, TextWriter output, TextWriter errorOutput)
    {
        if (args.Length == 0 || !string.Equals(args[0], "diff", StringComparison.OrdinalIgnoreCase))
        {
            errorOutput.WriteLine("Usage: openapi-report diff <oldSpecPath> <newSpecPath> [--format text|md|json] [--out <file>]");
            return 1;
        }

        if (args.Length < 3)
        {
            errorOutput.WriteLine("Error: missing required arguments.");
            errorOutput.WriteLine("Usage: openapi-report diff <oldSpecPath> <newSpecPath> [--format text|md|json] [--out <file>]");
            return 1;
        }

        var oldSpecPath = args[1];
        var newSpecPath = args[2];
        var format = "text";
        string? outputPath = null;

        for (var index = 3; index < args.Length; index++)
        {
            var current = args[index];
            if (string.Equals(current, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorOutput.WriteLine("Error: --format requires a value.");
                    return 1;
                }

                format = args[index + 1].ToLowerInvariant();
                index++;
                continue;
            }

            if (string.Equals(current, "--out", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    errorOutput.WriteLine("Error: --out requires a file path.");
                    return 1;
                }

                outputPath = args[index + 1];
                index++;
                continue;
            }

            errorOutput.WriteLine($"Error: unknown argument '{current}'.");
            return 1;
        }

        if (!File.Exists(oldSpecPath) || !File.Exists(newSpecPath))
        {
            errorOutput.WriteLine("Error: one or both spec files do not exist.");
            return 1;
        }

        try
        {
            var oldSpec = OpenApiParser.ParseFile(oldSpecPath);
            var newSpec = OpenApiParser.ParseFile(newSpecPath);

            var diffEngine = new SemanticDiffEngine();
            var changes = diffEngine.Diff(oldSpec, newSpec);
            var summary = ReportFormatters.BuildSummary(changes);

            var formattedOutput = format switch
            {
                "text" => ReportFormatters.FormatText(summary, changes),
                "md" => ReportFormatters.FormatMarkdown(summary, changes),
                "markdown" => ReportFormatters.FormatMarkdown(summary, changes),
                "json" => ReportFormatters.FormatJson(summary, changes),
                _ => throw new InvalidOperationException($"Unknown format '{format}'.")
            };

            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                File.WriteAllText(outputPath, formattedOutput);
                output.WriteLine(ReportFormatters.FormatSummary(summary));
            }
            else
            {
                output.WriteLine(formattedOutput);
            }

            return changes.Any(change => change.Severity == ChangeSeverity.Breaking) ? 2 : 0;
        }
        catch (Exception ex)
        {
            errorOutput.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
