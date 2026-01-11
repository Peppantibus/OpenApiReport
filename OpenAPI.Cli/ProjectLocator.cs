namespace OpenApiReport.Cli;

public static class ProjectLocator
{
    public static string ResolveProjectPath(string? projectPath, string searchRoot)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            return Path.GetFullPath(projectPath);
        }

        var slnMatches = Directory.EnumerateFiles(searchRoot, "*.sln", SearchOption.TopDirectoryOnly).ToList();
        if (slnMatches.Count == 1)
        {
            return slnMatches[0];
        }

        var projectMatches = Directory.EnumerateFiles(searchRoot, "*.csproj", SearchOption.AllDirectories).ToList();
        if (projectMatches.Count == 1)
        {
            return projectMatches[0];
        }

        if (projectMatches.Count == 0 && slnMatches.Count == 0)
        {
            throw new InvalidOperationException("Unable to find a project. Provide --project or add a config file.");
        }

        throw new InvalidOperationException("Multiple projects found. Provide --project or specify one in the config file.");
    }
}
