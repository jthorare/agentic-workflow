using Avalonia;

namespace Aw.Spike.E15;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
