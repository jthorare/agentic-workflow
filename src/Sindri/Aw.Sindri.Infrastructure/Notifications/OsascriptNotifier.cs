using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// macOS notifications through 'osascript' (engine spec §6.3: "shown as a macOS notification when one is available").
/// The title and text travel as script arguments ('on run argv'), never inside the script, so no text needs escaping.
/// Elsewhere, without osascript, or when it doesn't finish within 5 s, nothing is shown: the notifications file is the
/// record, and a notice is never failed for want of a banner.
/// </summary>
internal sealed class OsascriptNotifier : IOsNotifier
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(5);

    public async ValueTask Show(string title, string text, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var start = new ProcessStartInfo("osascript",
            ["-e", "on run argv", "-e", "display notification (item 2 of argv) with title (item 1 of argv)", "-e", "end run", title, text])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        try
        {
            using var osascript = Process.Start(start);
            if (osascript is not null)
            {
                await Wait(osascript, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Win32Exception)
        {
            // no osascript on PATH: no banner
        }
    }

    private static async Task Wait(Process osascript, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);
        try
        {
            var output = osascript.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = osascript.StandardError.ReadToEndAsync(deadline.Token);
            await osascript.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            osascript.Kill();   // never leave osascript running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
    }
}
