using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// §8's guards and filter (<see cref="QueueViewModel"/>, continued): while the pointer is over a panel new rows are held
/// behind an "N new" pill (spoken) instead of shifting the rows under it, while removals and updates show at once; for 500 ms after a refresh moved the selection on its
/// own, single keys are ignored; `/` filters the Queue page by id, title and repo; `Y` copies the selected row's CLI.
/// </summary>
internal sealed partial class QueueViewModel
{
    /// <summary>§8: how long single keys are ignored after focus moved on its own.</summary>
    internal static readonly TimeSpan KeyGuard = TimeSpan.FromMilliseconds(500);

    private bool _holding;
    private QueueView? _held;
    private DateTimeOffset? _movedAt;

    /// <summary>True while a held refresh brought rows the page doesn't show yet.</summary>
    public bool HasHeld => HeldCount > 0;

    /// <summary>"N new": the pill over the panel while rows are held.</summary>
    public string HeldText => Labels.NewRows(HeldCount);

    /// <summary>True for <see cref="KeyGuard"/> after a refresh moved the selection because its row cleared.</summary>
    public bool IsGuarded => _movedAt is { } at && clock.GetUtcNow() - at < KeyGuard;

    /// <summary>True while the Queue page's filter field is open (`/`).</summary>
    public bool IsFiltering { get; private set => Set(ref field, value); }

    /// <summary>The filter's text: rows whose id, title or repo contain it (any case) stay.</summary>
    public string FilterText
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            Show();
            ClearHidden();
        }
    } = string.Empty;

    private int HeldCount
    {
        get;
        set
        {
            if (field == value)
            {
                return;   // nothing changed: the window moves focus on when the pill hides, and must not on every refresh
            }

            field = value;
            OnChanged(nameof(HasHeld));
            OnChanged(nameof(HeldText));
        }
    }

    /// <summary>The pointer entered (true) or left (false) a panel; leaving shows what was held.</summary>
    /// <param name="holding">True while the pointer is over a panel.</param>
    internal void Hold(bool holding)
    {
        _holding = holding;
        if (!holding)
        {
            _ = Release();
        }
    }

    /// <summary>The pill: shows the held rows now, and moves focus to the first of them (the pill hides as it is pressed).</summary>
    /// <param name="scope">The page whose pill was pressed.</param>
    internal void ShowHeld(QueueScope scope)
    {
        var known = Navigable(scope).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        if (!Release(byPill: true))
        {
            return;
        }

        if (Navigable(scope).FirstOrDefault(r => !known.Contains(r.Id)) is { } first)
        {
            // The user asked for these rows: focus goes to the first, and the selection pushed off the page is no cleared row.
            Select(first.Id, scope);
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(first.Id, onlyIfLost: false));
        }
    }

    // Shows what is held; false when nothing was. The pill's press passes a selection it pushes off the page on without a word,
    // a focus move or the key guard: the user asked for the rows, and ShowHeld moves focus onto the first of them (the rows
    // it shows come in front of the selection, so a new row is always drawn).
    private bool Release(bool byPill = false)
    {
        if (_held is not { } view)
        {
            return false;
        }

        (_held, HeldCount) = (null, 0);
        Apply(view, byPill);
        return true;
    }

    /// <summary>`/`: opens the filter field.</summary>
    internal void OpenFilter() => IsFiltering = true;

    /// <summary>Esc on the filter: clears and closes it.</summary>
    internal void ClearFilter()
    {
        FilterText = string.Empty;
        IsFiltering = false;
    }

    /// <summary>`Y`: the selected row's primary action, when it has a CLI to copy.</summary>
    /// <returns>The control, or null.</returns>
    internal ControlViewModel? SelectedCopy() =>
        SelectedId is { } id && _rows.TryGetValue(id, out var row) && row.PrimaryAction is { CanCopy: true } control ? control : null;

    // While holding, only rows the page doesn't show yet and will draw wait behind the pill (they would shift the rows under the
    // pointer); removals, in-place updates and rows the page won't draw (past Home's cap, in a collapsed or filtered-out group)
    // show at once, so nothing stale sits under it. Returns what to show now.
    private QueueView Hold(QueueView view)
    {
        if (!_holding || _view is not { } shown)
        {
            (_held, HeldCount) = (null, 0);
            return view;
        }

        var ids = shown.Groups.SelectMany(g => g.Items).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var added = view.Groups.SelectMany(g => g.Items).Select(i => i.Id).Where(id => !ids.Contains(id)).ToHashSet(StringComparer.Ordinal);
        var drawn = DrawnIn(view, shown, _scope);
        var counted = added.Count(drawn.Contains);
        if (counted == 0)
        {
            (_held, HeldCount) = (null, 0);
            return view;
        }

        _held = view;
        if (HeldCount != counted)
        {
            HeldCount = counted;
            _spoken = HeldText;
        }

        return view with { Groups = [.. view.Groups.Select(g => g with { Items = [.. g.Items.Where(i => !added.Contains(i.Id))] })] };
    }

    // The ids a page would draw for a view (what Navigable reads off the shown rows): Home's two panels at their cap, or the
    // Queue page's picked, open groups' rows that pass the text filter. A picked group that is collapsed but has no rows in the
    // shown view has no heading yet, so a row that arrives in it brings the heading: the page moves, and the row counts.
    private HashSet<string> DrawnIn(QueueView view, QueueView shown, QueueScope scope)
    {
        if (scope != QueueScope.Home)
        {
            var headingless = shown.Groups.Where(g => g.Items.Count == 0).Select(g => g.Group).ToHashSet(StringComparer.Ordinal);
            return view.Groups.Where(g => Picked(g.Group) && (!_groups.TryGetValue(g.Group, out var open) || open.IsExpanded || headingless.Contains(g.Group)))
                .SelectMany(g => g.Items).Where(i => Matches(i.Id, i.Title, i.Repo ?? string.Empty))
                .Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        }

        var groups = view.Groups.ToDictionary(g => g.Group, g => g.Items, StringComparer.Ordinal);
        var left = (groups.GetValueOrDefault("withYou") ?? []).Concat(groups.GetValueOrDefault("running") ?? []).Take(PanelCap);
        return left.Concat((groups.GetValueOrDefault("queued") ?? []).Take(PanelCap)).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
    }

    // What this refresh has to say, so "N new" and "cleared" in one refresh are one sentence (Apply says it).
    private string? _spoken;

    private bool Matches(string id, string title, string repo) =>
        RepoMatches(repo) && (FilterText.Length == 0 || id.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
        || title.Contains(FilterText, StringComparison.OrdinalIgnoreCase) || repo.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
}
