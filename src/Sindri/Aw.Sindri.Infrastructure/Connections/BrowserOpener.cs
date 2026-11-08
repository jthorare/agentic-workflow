using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IBrowserOpener over 'open' (macOS) or 'xdg-open' (elsewhere), as the Needs-you links open (the URL is an argument, never a shell string).</summary>
internal sealed class BrowserOpener : IBrowserOpener
{
    // Starts the opener and returns: xdg-open can hold its terminal for as long as the browser runs, and the sign-in must not wait for it. The URL is an argument, never a shell string.
    public bool Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme is not ("https" or "http"))
        {
            return false;
        }

        var start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(url.AbsoluteUri);
        try
        {
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return false;   // no opener installed: the caller still hands the URL to the owner
        }
    }
}
