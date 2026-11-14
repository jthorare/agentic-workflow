using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's pinned Needs you group, order, search and asking-row jumps (<see cref="AgentsPageViewModel"/>, continued; #3082, #3153, #3024).
/// The asking rows leave their own group for Needs you, which is on top. Inside a group the rows follow the user's pick (name by default) and a read never
/// reorders them. An estimated state has to hold for <see cref="Hold"/> before a row shows it, so a guess does not flap Working, Idle, Working.
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>How long an estimated state has to stay before the row changes to it: 5 s, a little over one poll.</summary>
    internal static readonly TimeSpan Hold = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, (string Shown, (string State, DateTimeOffset At)? Candidate)> _held = new(StringComparer.Ordinal);

    /// <summary>Raised when <c>/</c> asks for the search box.</summary>
    internal event EventHandler? SearchRequested;

    /// <summary>The order the user picked; it holds across reads.</summary>
    public AgentSort Sort { get; private set => Set(ref field, value); }

    /// <summary>The search box's text: plain words that each match a row's name, repo, lane or folder.</summary>
    public string SearchText
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            OnChanged(nameof(IsFiltered));
            Redraw();
        }
    } = string.Empty;

    /// <summary>Picks the order of the rows.</summary>
    /// <param name="sort">The order.</param>
    internal void SetSort(AgentSort sort)
    {
        (Sort, SortDescending) = (sort, false);
        SortChanged();
    }

    /// <summary>True when the second click on the same header flipped the order.</summary>
    public bool SortDescending { get; private set => Set(ref field, value); }

    /// <summary>True while someone is asking: the head's "N asking you" is a link then.</summary>
    public bool HasAsking => Groups.Count > 0 && string.Equals(Groups[0].Name, Labels.NeedsYouGroup, StringComparison.Ordinal);

    /// <summary>The Status header, with the mark of the active sort.</summary>
    public string StatusHeader => Marked(AgentSort.State, Columns.Status);

    /// <summary>The Agent (name) header, with the mark of the active sort.</summary>
    public string AgentHeader => Marked(AgentSort.Name, Columns.Agent);

    /// <summary>The Repository header, with the mark of the active sort.</summary>
    public string RepoHeader => Marked(AgentSort.Repo, Columns.Repo);

    /// <summary>The For (age) header, with the mark of the active sort.</summary>
    public string ForHeader => Marked(AgentSort.Age, Columns.For);

    /// <summary>A header was clicked: sort by it, and a second click on the same one flips the direction.</summary>
    /// <param name="sort">The column's order.</param>
    internal void SortBy(AgentSort sort)
    {
        SortDescending = _picked && sort == Sort && !SortDescending;   // the default order (name) is not a click: the first one on its header sorts A to Z
        Sort = sort;
        SortChanged();
    }

    /// <summary>The head's link: selects the first row of the pinned Needs you group and asks for focus on it.</summary>
    /// <returns>True when someone is asking.</returns>
    internal bool FocusNeedsYou()
    {
        if (!HasAsking || Groups[0].Rows.Count == 0)
        {
            return false;
        }

        var key = Groups[0].Rows[0].Ref;
        _ = Select(key);
        RequestFocus(new AgentFocusEventArgs(key, toFocusLine: false, onlyIfLost: false));
        return true;
    }

    private bool _picked;

    // The default order (name) is unmarked; a header is marked once the user has picked a sort.
    private string Marked(AgentSort column, string header) => _picked && Sort == column ? header + (SortDescending ? " ▼" : " ▲") : header;

    private void SortChanged()
    {
        _picked = true;
        foreach (var name in (string[])[nameof(StatusHeader), nameof(AgentHeader), nameof(RepoHeader), nameof(ForHeader)])
        {
            OnChanged(name);
        }

        Redraw();
    }

    private List<SessionGroupViewModel> ShownGroups(List<SessionRowViewModel> kept)
    {
        var shown = new List<SessionGroupViewModel>();
        var asking = Ordered(kept.Where(r => r.IsAsking));
        var rest = kept.Where(r => !r.IsAsking).ToList();
        if (asking.Count > 0)
        {
            shown.Add(Group(Labels.NeedsYouGroup, asking));
        }

        var launched = Ordered(rest.Where(r => r.IsLaunched));
        var outside = Ordered(rest.Where(r => !r.IsLaunched));
        if (launched.Count > 0)
        {
            shown.Add(Group(Labels.StartedBySindri, launched));
        }

        if (outside.Count > 0)
        {
            shown.Add(Group(Labels.OutsideWorkflow, outside));
        }

        return shown;
    }

    private List<SessionRowViewModel> Ordered(IEnumerable<SessionRowViewModel> rows)
    {
        var byName = StringComparer.OrdinalIgnoreCase;
        IOrderedEnumerable<SessionRowViewModel> ordered = Sort switch
        {
            AgentSort.Repo => rows.OrderBy(r => r.Row.Repo ?? string.Empty, byName).ThenBy(r => r.TitleText, byName),
            AgentSort.State => rows.OrderBy(r => StateRank(r.Row.State)).ThenBy(r => r.TitleText, byName),
            AgentSort.Age => rows.OrderBy(r => r.Row.Since).ThenBy(r => r.TitleText, byName),
            AgentSort.Name => rows.OrderBy(r => r.TitleText, byName),
            _ => rows.OrderBy(r => r.TitleText, byName),
        };
        var sorted = ordered.ThenBy(r => r.Ref, StringComparer.Ordinal).ToList();
        if (SortDescending)
        {
            sorted.Reverse();
        }

        return sorted;
    }

    private static int StateRank(string state) => state switch
    {
        "asking" => 0,
        "running" => 1,
        "idle" => 2,
        _ => 3,
    };

    // Every word of the search has to be in the name, repo, lane or folder (any case).
    private bool MatchesSearch(SessionRowView row)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var haystack = string.Join('\n', SessionRowViewModel.TitleOf(row), row.Repo, row.Worktree, row.Lane?.Team, row.Lane?.Lane);
        return SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    // A served estimate that differs from what the row shows waits Hold before it shows; an exact state, or asking, shows at once.
    private SessionRowView Held(SessionRowView served)
    {
        if (!served.Estimated || string.Equals(served.State, "asking", StringComparison.Ordinal) || !_held.TryGetValue(served.Ref, out var kept))
        {
            _held[served.Ref] = (served.State, null);
            return served;
        }

        if (string.Equals(kept.Shown, served.State, StringComparison.Ordinal))
        {
            _held[served.Ref] = (kept.Shown, null);
            return served;
        }

        var since = kept.Candidate is { } c && string.Equals(c.State, served.State, StringComparison.Ordinal) ? c.At : Now;
        if (Now - since >= Hold)
        {
            _held[served.Ref] = (served.State, null);
            return served;
        }

        _held[served.Ref] = (kept.Shown, (served.State, since));
        return served with { State = kept.Shown, Asking = string.Equals(kept.Shown, "asking", StringComparison.Ordinal) ? served.Asking : null };
    }

    // Keys of this partial: / for the search box, ] and [ for the next and previous asking row (wrapping).
    private bool NeedsKey(string key)
    {
        switch (key)
        {
            case "/":
                SearchRequested?.Invoke(this, EventArgs.Empty);
                return true;
            case "]" or "[":
                return JumpAsking(forward: key == "]");
            default:
                return false;
        }
    }

    private bool JumpAsking(bool forward)
    {
        var asking = Groups.Where(g => string.Equals(g.Name, Labels.NeedsYouGroup, StringComparison.Ordinal)).SelectMany(g => g.Rows).Select(r => r.Ref).ToList();
        if (asking.Count == 0)
        {
            return false;
        }

        var at = asking.FindIndex(r => string.Equals(r, SelectedKey, StringComparison.Ordinal));
        var next = at < 0 ? (forward ? 0 : asking.Count - 1) : (at + (forward ? 1 : asking.Count - 1)) % asking.Count;
        return Select(asking[next]);
    }
}
