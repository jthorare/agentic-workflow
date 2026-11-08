using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IBrowserOpener over 'open' (macOS) or 'xdg-open' (elsewhere), as the Needs-you links open (the URL is an argument, never a shell string).</summary>
/// <param name="runner">Runs the opener.</param>
internal sealed class BrowserOpener(IToolRunner runner) : IBrowserOpener
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public bool Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme is not ("https" or "http"))
        {
            return false;
        }

        var run = runner.Run(OperatingSystem.IsMacOS() ? "open" : "xdg-open", [url.AbsoluteUri], _timeout);
        return run is { Outcome: ToolOutcome.Exited, ExitCode: 0 };
    }
}
