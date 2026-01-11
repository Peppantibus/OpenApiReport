namespace OpenApiReport.Cli;

public static class CaptureArgumentParser
{
    public static CaptureInput? Parse(
        string[] args,
        bool allowUnknownArguments,
        TextWriter errorOutput)
    {
        string? mode = null;
        string? outputPath = null;
        string? projectPath = null;
        string? configuration = null;
        string? framework = null;
        string? swaggerDoc = null;
        string? nswagConfig = null;
        string? url = null;
        string? configFilePath = null;
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

                mode = value;
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

            if (string.Equals(current, "--config-file", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadValue(args, ref index, errorOutput, out var value))
                {
                    return null;
                }

                configFilePath = value;
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

        return new CaptureInput
        {
            Mode = mode,
            OutputPath = outputPath,
            ConfigFilePath = configFilePath,
            ProjectPath = projectPath,
            Configuration = configuration,
            Framework = framework,
            SwaggerDoc = swaggerDoc,
            NswagConfigPath = nswagConfig,
            Url = url,
            Headers = headers
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

public sealed class CaptureInput
{
    public string? Mode { get; init; }
    public string? OutputPath { get; init; }
    public string? ConfigFilePath { get; init; }
    public string? ProjectPath { get; init; }
    public string? Configuration { get; init; }
    public string? Framework { get; init; }
    public string? SwaggerDoc { get; init; }
    public string? NswagConfigPath { get; init; }
    public string? Url { get; init; }
    public List<KeyValuePair<string, string>> Headers { get; init; } = new();
}
