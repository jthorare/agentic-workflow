using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IBrowserOpener over 'open' (macOS) or 'xdg-open' (elsewhere), as the Needs-you links open (the URL is an argument, never a shell string).</summary>
/// <param name="runner">Runs the opener.</param>
internal sealed class BrowserOpener(IToolRunner runner) : IBrowserOpener
{
    private readonly IToolRunner _runner = runner;

    public bool Open(Uri url) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _runner;
        return new NotSupportedException();
    }
}
