using Avalonia;

namespace Aw.Spike.T3335;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Hard rule: never let a spike process talk to an inherited tmux (a team's) server.
        Environment.SetEnvironmentVariable("TMUX", null);
        Environment.SetEnvironmentVariable("TMUX_PANE", null);
        return AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
