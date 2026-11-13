using System.ComponentModel;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

// Ending a session: kill-session, where a session that is already gone counts as stopped and an unknown failure does not.
internal sealed partial class TmuxSessionHost
{
    private static readonly TimeSpan _stopRetryPause = TimeSpan.FromMilliseconds(100);

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            var killed = await Run(["kill-session", "-t", Target(host)], cancellationToken, _callBudget).ConfigureAwait(false);
            if (killed.ExitCode != 0 && !IsGone(killed.Error))
            {
                // why (#3434): a server that is exiting (its last session just ended) answers "server exited unexpectedly" or "no current target";
                // asked once more it says "no server running". Only that second answer decides, so an unknown text is never taken as "gone".
                await Task.Delay(_stopRetryPause, cancellationToken).ConfigureAwait(false);
                killed = await Run(["kill-session", "-t", Target(host)], cancellationToken, _callBudget).ConfigureAwait(false);
            }

            return killed.ExitCode == 0 || IsGone(killed.Error)
                ? Unit.Value
                : KernelErrors.Unavailable.With($"tmux could not end {host.Name}: {Short(killed.Error)}");
        }
        catch (Win32Exception)
        {
            return Unit.Value;   // no tmux, so no session to end
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KernelErrors.Unavailable.With($"tmux did not answer while ending {host.Name}");
        }
    }

    private static bool IsGone(string error) =>
        error.Contains("can't find session", StringComparison.Ordinal)
        || error.Contains("no server running", StringComparison.Ordinal)
        || (error.Contains("error connecting to", StringComparison.Ordinal)
            && (error.Contains("(No such file or directory)", StringComparison.Ordinal) || error.Contains("(Connection refused)", StringComparison.Ordinal)));   // a permission error is not "gone"
}
