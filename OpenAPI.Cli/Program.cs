using OpenApiReport.Core;

namespace OpenApiReport.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var dispatcher = new OpenApiReportCommandDispatcher();
        return dispatcher.Execute(args, Console.Out, Console.Error);
    }
}
