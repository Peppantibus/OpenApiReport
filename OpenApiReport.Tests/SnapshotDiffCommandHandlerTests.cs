using OpenApiReport.Cli;

namespace OpenApiReport.Tests;

public class SnapshotDiffCommandHandlerTests
{
    [Fact]
    public void SnapshotDiff_SequencesGitCheckoutsAndWritesReports()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var outDir = Path.Combine(tempRoot, "reports");

        var gitClient = new FakeGitClient(tempRoot);
        var capture = new FakeCapture();
        var handler = new SnapshotDiffCommandHandler(gitClient, capture);

        var args = new[]
        {
            "snapshot-diff",
            "--mode",
            "url",
            "--url",
            "https://example.test/swagger.json",
            "--base-ref",
            "base",
            "--head-ref",
            "head",
            "--out-dir",
            outDir
        };

        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = handler.Execute(args, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { "base", "head", "main" }, gitClient.Checkouts);
        var projectName = new DirectoryInfo(tempRoot).Name;
        var reportDir = Path.Combine(outDir, projectName);
        Assert.True(File.Exists(Path.Combine(reportDir, "openapi.diff.md")));
        Assert.True(File.Exists(Path.Combine(reportDir, "openapi.diff.json")));
        Assert.Contains("breaking=", output.ToString());
    }

    [Fact]
    public void SnapshotDiff_UsesConfigFileDefaults()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var configPath = Path.Combine(tempRoot, "openapi-report.json");
        var outDir = Path.Combine(tempRoot, "reports");

        File.WriteAllText(configPath, """
        {
          "mode": "url",
          "url": "https://example.test/swagger.json",
          "snapshotDiff": {
            "baseRef": "base-config",
            "headRef": "head-config",
            "outDir": "reports",
            "projectName": "chat-api",
            "formats": [ "md", "json" ],
            "failOnBreaking": true
          }
        }
        """);

        var gitClient = new FakeGitClient(tempRoot);
        var capture = new FakeCapture();
        var handler = new SnapshotDiffCommandHandler(gitClient, capture);

        var args = new[]
        {
            "snapshot-diff",
            "--config-file",
            configPath
        };

        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = handler.Execute(args, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { "base-config", "head-config", "main" }, gitClient.Checkouts);
        var reportDir = Path.Combine(outDir, "chat-api");
        Assert.True(File.Exists(Path.Combine(reportDir, "openapi.diff.md")));
        Assert.True(File.Exists(Path.Combine(reportDir, "openapi.diff.json")));
    }

    private sealed class FakeGitClient : IGitClient
    {
        public FakeGitClient(string repoRoot)
        {
            RepositoryRoot = repoRoot;
        }

        public string RepositoryRoot { get; }
        public string CurrentRef { get; private set; } = "main";
        public List<string> Checkouts { get; } = new();

        public string GetRepositoryRoot() => RepositoryRoot;

        public string GetCurrentRef() => CurrentRef;

        public void Checkout(string gitRef)
        {
            CurrentRef = gitRef;
            Checkouts.Add(gitRef);
        }
    }

    private sealed class FakeCapture : IOpenApiSpecCapture
    {
        private const string OldSpec = """
        {
          "openapi": "3.0.0",
          "paths": { }
        }
        """;

        private const string NewSpec = """
        {
          "openapi": "3.0.0",
          "paths": {
            "/orders": {
              "get": {
                "tags": ["orders"],
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;

        public void Capture(CaptureOptions options, string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException("Output path is required.");
            }

            var content = outputPath.Contains("openapi.old.json", StringComparison.OrdinalIgnoreCase)
                ? OldSpec
                : NewSpec;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, content);
        }
    }
}
