using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The list's answer to an instance change (<see cref="WorkflowsViewModel"/>, continued).</summary>
internal sealed partial class WorkflowsViewModel
{
    /// <summary>Re-reads the list for a change of an instance (or for any change when <paramref name="change"/> is null).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="change">The frame, or null to re-read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>False when the row of that instance still isn't in the frame's state; true when it is, or when no row shows it.</returns>
    internal async Task<bool> Catch(Uri url, InstanceChange? change, CancellationToken cancellationToken)
    {
        await Refresh(url, cancellationToken).ConfigureAwait(true);
        if (change is null)
        {
            return true;
        }

        var row = Groups.SelectMany(g => g.Rows).Concat(DoneRows).FirstOrDefault(r => string.Equals(r.Address, change.Instance, StringComparison.Ordinal));
        return row is null || string.Equals(row.StateWord, change.State, StringComparison.Ordinal);   // no row: a child, or one the filters leave out; nothing to wait for
    }

    // Time passes between reads: the "in stage" and "ended ago" words are read anew from the clock.
    private void TickTimes()
    {
        foreach (var row in _loaded.Concat(DoneRows))
        {
            row.Tick();
        }
    }
}
