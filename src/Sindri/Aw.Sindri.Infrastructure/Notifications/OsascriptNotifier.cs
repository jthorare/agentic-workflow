using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// macOS notifications through 'osascript' (engine spec §6.3: "shown as a macOS notification when one is available").
/// The title and text travel as script arguments ('on run argv'), never inside the script, so no text needs escaping.
/// An osascript banner has no click action, so when 'terminal-notifier' is on PATH and the notice has a deep link, the banner
/// goes through it and a click runs 'sindri-ui &lt;link&gt;' (see <see cref="NotifierCommand"/>); without it the banner is the same as before.
/// Elsewhere, without osascript, or when it doesn't finish within 5 s, nothing is shown: the notifications file is the
/// record, and a notice is never failed for want of a banner.
/// </summary>
internal sealed class OsascriptNotifier : IOsNotifier
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(5);

    public async ValueTask Show(string title, string text, string? deepLink, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var command = NotifierCommand.For(title, text, deepLink, NotifierCommand.OnPath("terminal-notifier"), NotifierCommand.UiBinary(Environment.GetEnvironmentVariable, AppContext.BaseDirectory));
        var start = new ProcessStartInfo(command.File, command.Arguments)
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
