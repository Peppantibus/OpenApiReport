using System.Text;
using System.Text.Json;

namespace OpenApiReport.Cli;

public interface IOpenApiSpecCapture
{
    void Capture(CaptureOptions options, string? outputPath);
}

public sealed class OpenApiSpecCapture : IOpenApiSpecCapture
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private readonly IProcessRunner _processRunner;

    public OpenApiSpecCapture(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public void Capture(CaptureOptions options, string? outputPath)
    {
        switch (options.Mode)
        {
            case CaptureMode.Swashbuckle:
                CaptureWithSwashbuckle(options, outputPath);
                break;
            case CaptureMode.Nswag:
                CaptureWithNswag(options, outputPath);
                break;
            case CaptureMode.Url:
                CaptureFromUrl(options, outputPath);
                break;
            default:
                throw new InvalidOperationException($"Unsupported capture mode '{options.Mode}'.");
        }
    }

    private void CaptureWithSwashbuckle(CaptureOptions options, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Swashbuckle capture requires --out <file>.");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectPath))
        {
            throw new InvalidOperationException("Swashbuckle capture requires --project <path-to-csproj-or-sln>.");
        }

        var projectPath = ResolveProjectPath(options.ProjectPath);
        BuildProject(projectPath, options.Configuration, options.Framework);

        var assemblyPath = ResolveAssemblyPath(projectPath, options.Configuration, options.Framework);
        EnsureDirectory(outputPath);

        RunProcess(
            "dotnet",
            new[]
            {
                "swagger",
                "tofile",
                "--output",
                outputPath,
                assemblyPath,
                options.SwaggerDoc
            });

        EnsureFileExists(outputPath, "Swashbuckle output was not generated.");
        RewriteAsUtf8(outputPath);
    }

    private void CaptureWithNswag(CaptureOptions options, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(options.NswagConfigPath))
        {
            throw new InvalidOperationException("NSwag capture requires --config <path-to-nswag.json|nswag.yaml>.");
        }

        var configPath = options.NswagConfigPath;
        var workingDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath));
        var arguments = new List<string> { "run", configPath };
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            arguments.Add($"/variables:Output={outputPath}");
        }

        RunProcess("nswag", arguments, workingDirectory);

        var resolvedOutput = outputPath ?? TryResolveNswagOutput(configPath);
        if (string.IsNullOrWhiteSpace(resolvedOutput))
        {
            throw new InvalidOperationException("NSwag output file was not specified. Provide --output or set output in the config.");
        }

        EnsureFileExists(resolvedOutput, "NSwag output was not generated.");
        RewriteAsUtf8(resolvedOutput);
    }

    private void CaptureFromUrl(CaptureOptions options, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("URL capture requires --out <file>.");
        }

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            throw new InvalidOperationException("URL capture requires --url <https://.../swagger.json>.");
        }

        EnsureDirectory(outputPath);
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, options.Url);
        foreach (var header in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var response = httpClient.Send(request);
        response.EnsureSuccessStatusCode();

        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        File.WriteAllText(outputPath, content, Utf8NoBom);
    }

    private string ResolveProjectPath(string projectPath)
    {
        var fullPath = Path.GetFullPath(projectPath);
        if (fullPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSolutionProject(fullPath);
        }

        return fullPath;
    }

    private string ResolveSolutionProject(string solutionPath)
    {
        var result = RunProcess("dotnet", new[] { "sln", solutionPath, "list" });
        var projectLine = result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.TrimEnd().EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(projectLine))
        {
            throw new InvalidOperationException($"Unable to locate a project in solution '{solutionPath}'.");
        }

        var projectPath = projectLine.Trim();
        if (!Path.IsPathRooted(projectPath))
        {
            projectPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solutionPath) ?? string.Empty, projectPath));
        }

        return projectPath;
    }

    private void BuildProject(string projectPath, string configuration, string? framework)
    {
        var arguments = new List<string> { "build", projectPath, "-c", configuration };
        if (!string.IsNullOrWhiteSpace(framework))
        {
            arguments.AddRange(new[] { "-f", framework });
        }

        RunProcess("dotnet", arguments);
    }

    private string ResolveAssemblyPath(string projectPath, string configuration, string? framework)
    {
        var arguments = new List<string>
        {
            "msbuild",
            projectPath,
            "-getProperty:TargetPath",
            $"-property:Configuration={configuration}"
        };

        if (!string.IsNullOrWhiteSpace(framework))
        {
            arguments.Add($"-property:TargetFramework={framework}");
        }

        var result = RunProcess("dotnet", arguments);
        var targetLine = result.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("TargetPath", StringComparison.OrdinalIgnoreCase));

        if (targetLine is not null)
        {
            var split = targetLine.Split('=', 2);
            if (split.Length == 2)
            {
                var path = split[1].Trim();
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        var projectDirectory = Path.GetDirectoryName(projectPath) ?? Environment.CurrentDirectory;
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var searchRoot = Path.Combine(projectDirectory, "bin", configuration);
        if (!string.IsNullOrWhiteSpace(framework))
        {
            searchRoot = Path.Combine(searchRoot, framework);
        }

        if (Directory.Exists(searchRoot))
        {
            var candidate = Directory.EnumerateFiles(searchRoot, $"{projectName}.dll", SearchOption.AllDirectories)
                .FirstOrDefault(path => !path.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            if (candidate is not null)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unable to locate the built assembly for '{projectPath}'.");
    }

    private string? TryResolveNswagOutput(string configPath)
    {
        var extension = Path.GetExtension(configPath).ToLowerInvariant();
        var content = File.ReadAllText(configPath);
        if (extension is ".json")
        {
            using var document = JsonDocument.Parse(content);
            if (TryFindJsonOutput(document.RootElement, out var output))
            {
                return ResolveConfigRelativePath(configPath, output);
            }
        }
        else if (extension is ".yaml" or ".yml")
        {
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("output:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = trimmed["output:".Length..].Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return ResolveConfigRelativePath(configPath, value);
                    }
                }
            }
        }

        return null;
    }

    private static bool TryFindJsonOutput(JsonElement element, out string? output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "output", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    output = property.Value.GetString();
                    return !string.IsNullOrWhiteSpace(output);
                }

                if (TryFindJsonOutput(property.Value, out output))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindJsonOutput(item, out output))
                {
                    return true;
                }
            }
        }

        output = null;
        return false;
    }

    private static string ResolveConfigRelativePath(string configPath, string value)
    {
        if (Path.IsPathRooted(value))
        {
            return value;
        }

        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(baseDirectory, value));
    }

    private ProcessResult RunProcess(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var result = _processRunner.Run(fileName, arguments, workingDirectory);
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            throw new InvalidOperationException($"{fileName} command failed: {message}".Trim());
        }

        return result;
    }

    private static void EnsureDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void EnsureFileExists(string outputPath, string errorMessage)
    {
        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static void RewriteAsUtf8(string outputPath)
    {
        var content = File.ReadAllText(outputPath);
        File.WriteAllText(outputPath, content, Utf8NoBom);
    }
}
