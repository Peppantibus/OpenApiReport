namespace OpenApiReport.Cli;

public sealed class CaptureCommandHandler
{
    private readonly IOpenApiSpecCapture _capture;

    public CaptureCommandHandler()
        : this(new OpenApiSpecCapture(new ProcessRunner()))
    {
    }

    public CaptureCommandHandler(IOpenApiSpecCapture capture)
    {
        _capture = capture;
    }

    public int Execute(string[] args, TextWriter output, TextWriter errorOutput)
    {
        if (args.Length == 0 || !string.Equals(args[0], "capture", StringComparison.OrdinalIgnoreCase))
        {
            errorOutput.WriteLine("Usage: openapi-report capture [--mode <swashbuckle|nswag|url>] [--config-file <path>] --out <file> [options]");
            return 1;
        }

        try
        {
            var input = CaptureArgumentParser.Parse(args, allowUnknownArguments: false, errorOutput);
            if (input is null)
            {
                return 1;
            }

            var config = OpenApiReportConfigLoader.LoadIfExists(input.ConfigFilePath, Environment.CurrentDirectory);
            var modeValue = input.Mode ?? config?.Mode ?? "swashbuckle";
            if (!Enum.TryParse<CaptureMode>(modeValue, ignoreCase: true, out var mode))
            {
                errorOutput.WriteLine($"Error: unknown mode '{modeValue}'.");
                return 1;
            }

            var outputPath = input.OutputPath ?? config?.Output;
            if (mode != CaptureMode.Nswag && string.IsNullOrWhiteSpace(outputPath))
            {
                errorOutput.WriteLine("Error: --out is required.");
                return 1;
            }

            var options = new CaptureOptions
            {
                Mode = mode,
                ProjectPath = input.ProjectPath ?? config?.Project,
                Configuration = input.Configuration ?? config?.Configuration ?? "Release",
                Framework = input.Framework ?? config?.Framework,
                SwaggerDoc = input.SwaggerDoc ?? config?.SwaggerDoc ?? "v1",
                NswagConfigPath = input.NswagConfigPath ?? config?.NswagConfig,
                Url = input.Url ?? config?.Url,
                Headers = input.Headers.Count > 0
                    ? input.Headers
                    : config?.Headers?.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value)).ToList()
                    ?? new List<KeyValuePair<string, string>>()
            };

            _capture.Capture(options, outputPath);
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                output.WriteLine($"Captured OpenAPI spec to {outputPath}.");
            }
            else
            {
                output.WriteLine("Captured OpenAPI spec.");
            }
            return 0;
        }
        catch (Exception ex)
        {
            errorOutput.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
