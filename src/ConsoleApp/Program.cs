using ConsoleApp.UI;

namespace ConsoleApp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var shell = new ConsoleShell();
        await shell.RunAsync(args);
        return 0;
    }
}
