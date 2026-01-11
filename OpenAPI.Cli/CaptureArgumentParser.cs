namespace OpenApiReport.Cli;

public static class CaptureArgumentParser
{
    public static (CaptureOptions Options, string? OutputPath)? Parse(
        string[] args,
        bool requireOutput,
        bool allowUnknownArguments,
        TextWriter errorOutput)
    {
        CaptureMode? mode = null;
        string? outputPath = null;
        string? projectPath = null;
        string configuration = "Release";
        string? framework = null;
        string swaggerDoc = "v1";
        string? nswagConfig = null;
        string? url = null;
        var headers = new List<KeyValuePair<string, string>>();

        for (var index = 1; index < args.Length; index++)
        {
            var current = args[index];
            if (string.Equals(current, "--mode", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                if (!Enum.TryParse<CaptureMode>(value, ignoreCase: true, out var parsedMode))
                {
                    errorOutput.WriteLine($"Error: unknown mode '{value}'.");
                    return null;
                }

                mode = parsedMode;
                continue;
            }

            if (string.Equals(current, "--out", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                outputPath = value;
                continue;
            }

            if (string.Equals(current, "--project", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                projectPath = value;
                continue;
            }

            if (string.Equals(current, "--configuration", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                configuration = value;
                continue;
            }

            if (string.Equals(current, "--framework", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                framework = value;
                continue;
            }

            if (string.Equals(current, "--swaggerDoc", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                swaggerDoc = value;
                continue;
            }

            if (string.Equals(current, "--config", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                nswagConfig = value;
                continue;
            }

            if (string.Equals(current, "--url", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                url = value;
                continue;
            }

            if (string.Equals(current, "--header", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                var split = value.Split(':', 2, StringSplitOptions.TrimEntries);
                if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]))
                {
                    errorOutput.WriteLine("Error: --header expects format 'Key:Value'.");
                    return null;
                }

                headers.Add(new KeyValuePair<string, string>(split[0], split[1]));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(current) && !allowUnknownArguments)
            {
                errorOutput.WriteLine($"Error: unknown argument '{current}'.");
                return null;
            }
        }

        if (!mode.HasValue)
        {
            errorOutput.WriteLine("Error: --mode is required.");
            return null;
        }

        if (requireOutput && string.IsNullOrWhiteSpace(outputPath))
        {
            errorOutput.WriteLine("Error: --out is required.");
            return null;
        }

        var options = new CaptureOptions
        {
            Mode = mode.Value,
            ProjectPath = projectPath,
            Configuration = configuration,
            Framework = framework,
            SwaggerDoc = swaggerDoc,
            NswagConfigPath = nswagConfig,
            Url = url,
            Headers = headers
        };

        return (options, outputPath);
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
