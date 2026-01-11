using OpenApiReport.Core;

namespace OpenApiReport.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var handler = new DiffCommandHandler();
        return handler.Execute(args, Console.Out, Console.Error);
    }
}
