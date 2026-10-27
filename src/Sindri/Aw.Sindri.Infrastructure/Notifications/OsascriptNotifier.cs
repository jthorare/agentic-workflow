using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// macOS notifications (engine spec §6.3: "shown as a macOS notification when one is available") through 'terminal-notifier',
/// whose click runs 'sindri-ui &lt;link&gt;' (see <see cref="NotifierCommand"/>). An osascript banner does have a click action, but it
/// opens Script Editor, not the app, so none is posted: without terminal-notifier or a deep link, elsewhere, or when it doesn't
/// finish within 5 s, nothing is shown. The notifications file is the record, and a notice is never failed for want of a banner.
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

        var notifier = NotifierCommand.OnPath("terminal-notifier");
        if (notifier is null || deepLink is null || !NotifierCommand.Shows(deepLink, notifier))
        {
            return;
        }

        var command = NotifierCommand.For(title, text, deepLink, notifier, NotifierCommand.UiBinary(Environment.GetEnvironmentVariable, AppContext.BaseDirectory));
        var start = ChildEnvironment.Start(command.File, command.Arguments);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        try
        {
            using var banner = Process.Start(start);
            if (banner is not null)
            {
                await Wait(banner, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Win32Exception)
        {
            // terminal-notifier vanished since the PATH lookup: no banner
        }
    }

    private static async Task Wait(Process banner, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);
        try
        {
            var output = banner.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = banner.StandardError.ReadToEndAsync(deadline.Token);
            await banner.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            _ = await output.ConfigureAwait(false);
            _ = await error.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            banner.Kill();   // never leave the banner process running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
    }
}
