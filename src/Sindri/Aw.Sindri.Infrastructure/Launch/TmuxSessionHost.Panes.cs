using System.ComponentModel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

internal sealed partial class TmuxSessionHost
{
    /// <inheritdoc/>
    public string Socket => options.Socket;

    /// <inheritdoc/>
    public async ValueTask<TmuxPaneId?> PaneId(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            var listed = await Run(["list-panes", "-t", Target(host), "-F", "#{pane_id}"], cancellationToken, _callBudget).ConfigureAwait(false);
            return listed.ExitCode == 0 && listed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is [{ } pane, ..] && TmuxPaneId.TryFrom(pane, out var id) ? id : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool?> Running(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            var listed = await Run(["list-panes", "-t", Target(host), "-F", "#{pane_dead}"], cancellationToken, _callBudget).ConfigureAwait(false);
            if (listed.ExitCode != 0)
            {
                return IsGone(listed.Error) ? false : null;   // why: a session tmux can't find has Ended; any other failure is "can't tell", which keeps the tree
            }

            return listed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is not ["1", ..];
        }
        catch (Win32Exception)
        {
            return false;   // no tmux, so no session runs
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
