namespace OpenApiReport.Cli;

public sealed class OpenApiReportCommandDispatcher
{
    private readonly DiffCommandHandler _diffHandler = new();
    private readonly CaptureCommandHandler _captureHandler = new();
    private readonly SnapshotDiffCommandHandler _snapshotDiffHandler = new();

    public int Execute(string[] args, TextWriter output, TextWriter errorOutput)
    {
        if (args.Length == 0)
        {
            errorOutput.WriteLine("Usage: openapi-report <diff|capture|snapshot-diff> [options]");
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "diff" => _diffHandler.Execute(args, output, errorOutput),
            "capture" => _captureHandler.Execute(args, output, errorOutput),
            "snapshot-diff" => _snapshotDiffHandler.Execute(args, output, errorOutput),
            _ => WriteUnknownCommand(command, errorOutput)
        };
    }

    private static int WriteUnknownCommand(string command, TextWriter errorOutput)
    {
        errorOutput.WriteLine($"Error: unknown command '{command}'.");
        errorOutput.WriteLine("Usage: openapi-report <diff|capture|snapshot-diff> [options]");
        return 1;
    }
}
