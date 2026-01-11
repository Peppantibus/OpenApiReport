namespace OpenApiReport.Cli;

public interface IGitClient
{
    string GetRepositoryRoot();
    string GetCurrentRef();
    void Checkout(string gitRef);
}

public sealed class ProcessGitClient : IGitClient
{
    private readonly IProcessRunner _processRunner;
    private readonly string _workingDirectory;

    public ProcessGitClient(IProcessRunner processRunner, string? workingDirectory = null)
    {
        _processRunner = processRunner;
        _workingDirectory = workingDirectory ?? Environment.CurrentDirectory;
    }

    public string GetRepositoryRoot()
    {
        var result = RunGit(new[] { "rev-parse", "--show-toplevel" });
        return result.StandardOutput.Trim();
    }

    public string GetCurrentRef()
    {
        var branchResult = RunGit(new[] { "rev-parse", "--abbrev-ref", "HEAD" });
        var branch = branchResult.StandardOutput.Trim();
        if (!string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return branch;
        }

        var shaResult = RunGit(new[] { "rev-parse", "HEAD" });
        return shaResult.StandardOutput.Trim();
    }

    public void Checkout(string gitRef)
    {
        RunGit(new[] { "checkout", gitRef });
    }

    private ProcessResult RunGit(IReadOnlyList<string> arguments)
    {
        var result = _processRunner.Run("git", arguments, _workingDirectory);
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            throw new InvalidOperationException($"Git command failed: {message}".Trim());
        }

        return result;
    }
}
