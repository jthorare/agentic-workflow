namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The row of a session just started (<see cref="AgentsPageViewModel"/>, continued; #3384): selected when the list draws it, and kept while its row id changes.</summary>
internal sealed partial class AgentsPageViewModel
{
    // The tmux session name, sindri-<actId>: the start's id until the agent writes its fleet file, and the row's Host.Session before and after (agents spec §15.8).
    private string? _started;

    // The key this followed the row to last; a selection elsewhere than that, while that row is still drawn, is the person's and ends the following.
    private string? _followed;

    /// <summary>Selects the row of a session just started, now if it is drawn and else when a read draws it; the selection follows the row when its id changes (#3384).</summary>
    /// <param name="session">The start's session name, sindri-&lt;actId&gt;.</param>
    internal void SelectStarted(string session)
    {
        (_started, _followed) = (session, null);
        FollowStarted();
    }

    // After a read drew its rows. The row id changes from sindri-<actId> to the provider's own when the fleet file lands, and the read
    // moves the selection to a neighbour when the old key is gone, so the row is found again by its session name.
    private void FollowStarted()
    {
        if (_started is not { } session)
        {
            return;
        }

        if (_followed is { } last && !string.Equals(SelectedKey, last, StringComparison.Ordinal) && WalkKeys().Contains(last, StringComparer.Ordinal))
        {
            _started = null;   // the person moved to another row while this one stayed
            return;
        }

        var row = Groups.Where(g => g.IsExpanded).SelectMany(g => g.Rows)
            .FirstOrDefault(r => string.Equals(r.Row.Host.Session, session, StringComparison.Ordinal) || string.Equals(r.Row.Id, session, StringComparison.Ordinal));
        if (row is not null && Select(row.Ref))
        {
            _followed = row.Ref;
        }
    }
}
