namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's keys (<see cref="AgentsPageViewModel"/>, continued; collector spec CU-1, Q6): J / K walk the drawn rows, Enter opens the
/// selected session's sheet, O opens the frame of the Step it works on (else moves focus to its Focus line), Y copies the selected ended row's resume command (or the Focus line's CLI) and
/// E folds the recently ended. The selection is a key: a re-read keeps it on the same row and moves it to the neighbour when its row is gone.
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>The key of the row the keys act on (a live row's ref, an ended row's key); null for none.</summary>
    public string? SelectedKey { get; private set => Set(ref field, value); }

    // The rows J / K walk: those drawn (a collapsed group's are not), then the ended rows while the fold is open.
    private List<(string Key, SessionRowViewModel? Live, EndedSessionViewModel? Ended)> Walk() =>
    [
        .. Groups.Where(g => g.IsExpanded).SelectMany(g => g.Rows).Select(r => (r.Ref, (SessionRowViewModel?)r, (EndedSessionViewModel?)null)),
        .. IsEndedExpanded ? Ended.Select(e => (e.Key, (SessionRowViewModel?)null, (EndedSessionViewModel?)e)) : [],
    ];

    private List<string> WalkKeys() => [.. Walk().Select(w => w.Key)];

    private SessionRowViewModel? SelectedLive => Walk().Where(w => string.Equals(w.Key, SelectedKey, StringComparison.Ordinal)).Select(w => w.Live).FirstOrDefault();

    private EndedSessionViewModel? SelectedEnded => Walk().Where(w => string.Equals(w.Key, SelectedKey, StringComparison.Ordinal)).Select(w => w.Ended).FirstOrDefault();

    /// <summary>Drops the selection (focus left the list and the sheet is closed): no row keeps its left bar.</summary>
    internal void ClearSelection()
    {
        if (SelectedKey is null)
        {
            return;
        }

        SelectedKey = null;
        Mark();
    }

    /// <summary>Selects a row by its key (focus arrived on it).</summary>
    /// <param name="key">The row's key.</param>
    /// <returns>True when such a row is drawn.</returns>
    internal bool Select(string key)
    {
        if (!Walk().Exists(w => string.Equals(w.Key, key, StringComparison.Ordinal)))
        {
            return false;
        }

        SelectedKey = key;
        Mark();
        return true;
    }

    /// <summary>One single key on the page.</summary>
    /// <param name="key">J, K, Enter, O, Y or E.</param>
    /// <returns>True when the key was used.</returns>
    internal bool Press(string key) => key switch
    {
        "J" => Move(1),
        "K" => Move(-1),
        "E" => HasEnded && Toggled(),
        "Enter" or "O" => PressOnLive(key, SelectedLive),
        "Y" => Copied(),
        _ => false,
    };

    private bool Toggled()
    {
        ToggleEnded();
        return true;
    }

    // Enter opens the selected session's sheet; O opens the frame of the Step it works on, else puts keyboard focus on its Focus line (the send itself arrives in U11a-f).
    private bool PressOnLive(string key, SessionRowViewModel? row)
    {
        if (row is null)
        {
            return false;
        }

        if (key == "Enter")
        {
            RequestOpen(new SessionOpenRequestedEventArgs(row.Link));
        }
        else if (row.HasStep)
        {
            row.OpenFrame();   // O: open with the resolved target, the frame of the Step the session works on
        }
        else
        {
            RequestFocus(new AgentFocusEventArgs(row.Ref, toFocusLine: true, onlyIfLost: false));
        }

        return true;
    }

    // Y copies the selected ended row's resume command, or the selected live row's Focus CLI.
    private bool Copied()
    {
        if (SelectedLive is { } live)
        {
            _ = live.Focus.Copy(CancellationToken.None);
            return true;
        }

        if (SelectedEnded is { } ended)
        {
            _ = ended.Copy(CancellationToken.None);
            return true;
        }

        return false;
    }

    // From no row J goes to the first and K to the last; at either end the selection stays.
    private bool Move(int step)
    {
        var keys = WalkKeys();
        if (keys.Count == 0)
        {
            return false;
        }

        var at = keys.FindIndex(k => string.Equals(k, SelectedKey, StringComparison.Ordinal));
        var to = at >= 0 ? Math.Clamp(at + step, 0, keys.Count - 1) : (step > 0 ? 0 : keys.Count - 1);
        SelectedKey = keys[to];
        Mark();
        RequestFocus(new AgentFocusEventArgs(keys[to], toFocusLine: false, onlyIfLost: false));
        return true;
    }

    // After a read: the selection stays on the same row (and asks for focus back if the re-read took it away with the old rows); when its
    // row is gone it moves to the row that followed it (the one before it at the end), never to the top.
    private void Reselect(IReadOnlyList<string> before)
    {
        if (SelectedKey is { } at)
        {
            var now = WalkKeys();
            if (!now.Contains(at, StringComparer.Ordinal))
            {
                var i = before.ToList().FindIndex(k => string.Equals(k, at, StringComparison.Ordinal));
                SelectedKey = i < 0 ? null : before.Skip(i + 1).FirstOrDefault(now.Contains) ?? before.Take(i).Reverse().FirstOrDefault(now.Contains);
            }

            if (SelectedKey is { } still)
            {
                RequestFocus(new AgentFocusEventArgs(still, toFocusLine: false, onlyIfLost: true));
            }
        }

        Mark();
    }

    private void Mark()
    {
        foreach (var (key, live, ended) in Walk())
        {
            var selected = string.Equals(key, SelectedKey, StringComparison.Ordinal);
            live?.IsSelected = selected;
            ended?.IsSelected = selected;
        }
    }
}
