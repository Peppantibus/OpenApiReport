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
            errorOutput.WriteLine("Usage: openapi-report capture --mode <swashbuckle|nswag|url> --out <file> [options]");
            return 1;
        }

        try
        {
            var parseResult = CaptureArgumentParser.Parse(args, requireOutput: false, allowUnknownArguments: false, errorOutput);
            if (parseResult is null)
            {
                return 1;
            }

            var outputPath = parseResult.Value.OutputPath;
            if (parseResult.Value.Options.Mode != CaptureMode.Nswag && string.IsNullOrWhiteSpace(outputPath))
            {
                errorOutput.WriteLine("Error: --out is required.");
                return 1;
            }

            _capture.Capture(parseResult.Value.Options, outputPath);
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
