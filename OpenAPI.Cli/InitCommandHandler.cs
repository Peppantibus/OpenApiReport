using System.Text.Json;

namespace OpenApiReport.Cli;

public sealed class InitCommandHandler
{
    public int Execute(string[] args, TextWriter output, TextWriter errorOutput)
    {
        if (args.Length == 0 || !string.Equals(args[0], "init", StringComparison.OrdinalIgnoreCase))
        {
            errorOutput.WriteLine("Usage: openapi-report init [--config-file <path>] [--mode <swashbuckle|nswag|url>] [--project <path>]");
            return 1;
        }

        try
        {
            var input = CaptureArgumentParser.Parse(args, allowUnknownArguments: true, errorOutput);
            if (input is null)
            {
                return 1;
            }

            var modeValue = input.Mode ?? "swashbuckle";
            if (!Enum.TryParse<CaptureMode>(modeValue, ignoreCase: true, out var mode))
            {
                errorOutput.WriteLine($"Error: unknown mode '{modeValue}'.");
                return 1;
            }

            var projectPath = input.ProjectPath;
            if (mode == CaptureMode.Swashbuckle)
            {
                projectPath = ProjectLocator.ResolveProjectPath(projectPath, Environment.CurrentDirectory);
            }

            var config = new OpenApiReportConfig
            {
                Mode = mode.ToString().ToLowerInvariant(),
                Project = projectPath,
                Configuration = "Release",
                SwaggerDoc = "v1",
                SnapshotDiff = new SnapshotDiffConfig
                {
                    BaseRef = "origin/main",
                    HeadRef = "HEAD",
                    OutDir = "./reports/openapi",
                    Formats = new List<string> { "md", "json" },
                    FailOnBreaking = true
                }
            };

            var path = OpenApiReportConfigLoader.ResolveConfigPathOrDefault(input.ConfigFilePath, Environment.CurrentDirectory);
            if (File.Exists(path))
            {
                errorOutput.WriteLine($"Error: config file already exists at {path}.");
                return 1;
            }
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
            output.WriteLine($"Wrote config to {path}.");
            return 0;
        }
        catch (Exception ex)
        {
            errorOutput.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
