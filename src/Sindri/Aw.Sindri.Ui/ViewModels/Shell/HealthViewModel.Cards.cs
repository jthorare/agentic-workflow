using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The System overview's Agents, Views, Code index and Context index cards and what they add to the headline (<see cref="HealthViewModel"/>, continued).</summary>
internal sealed partial class HealthViewModel
{
    private void ShowAgentsCard()
    {
        var s = Sessions;
        AgentsRows = s is null
            ? []
            : [new(Labels.AgentsWorking, Count(s.Running ?? s.Live)), new(Labels.AgentsAsking, Count(s.Asking)), new(Labels.AgentsProblems, Count(s.Problems))];
        IsAgentsWatchingOff = string.Equals(s?.Watching, "off", StringComparison.Ordinal);   // #4316: the owner's choice, shown in place of problems
        HasAgentsProblems = !IsAgentsWatchingOff && s is { Problems: > 0 };   // an older answer has the count and no rows: the link still shows (#4012)
        AgentsProblemLines = HasAgentsProblems && s is { ProblemRows: { } rows } ? [.. rows.Select(static p => new AgentProblemLine(p.Text, p.Code))] : [];
    }

    private void ShowViewsCard()
    {
        var views = _serviceList?.AsOf;
        ViewsWord = views switch
        {
            null => Labels.ViewsUnknown,
            { Stalled: true } => Labels.ViewsStalled,
            { Rebuilding: true } => Labels.ViewsCardRebuilding,
            _ => Labels.ViewsUpToDate,
        };
        ViewsGlyph = views switch
        {
            null => StateGlyph.None,
            { Stalled: true } => StateGlyph.Down,
            { Rebuilding: true } => StateGlyph.None,
            _ => StateGlyph.Ok,
        };
        ViewsRows = views is null ? [] : ViewsCells(views);
        ViewsFix = views is { } v && (v.Stalled || v.Rebuilding) ? Catalog.Rebuild : null;
    }

    /// <summary>
    /// Has the Code index and Context index cards read on connect and after every event, with the other views, while they are shown
    /// (#2775: they and the headline they feed are on the System page only); <see cref="ReadCards"/> reads them when it opens.
    /// </summary>
    /// <param name="shown">True while the cards are on screen.</param>
    internal void FollowCards(Func<bool> shown)
    {
        Follow((url, token) => shown() ? CodeIndex.Refresh(url, token) : Task.CompletedTask);
        Follow((url, token) => shown() ? ContextIndex.Refresh(url, token) : Task.CompletedTask);
    }

    // Cancels and frees the cards' read in flight; an Action so the view model owns no disposable field.
    private Action? _stopCards;

    /// <summary>Reads both cards once now (the page they are on was opened, so they are never stale on open); nothing while not connected or Not running.</summary>
    internal void ReadCards()
    {
        StopCards();
        if (_url is not { } url || IsNotRunning)
        {
            return;
        }

#pragma warning disable CA2000 // disposed by _stopCards
        var read = new CancellationTokenSource();
#pragma warning restore CA2000
        _stopCards = () =>
        {
            read.Cancel();
            read.Dispose();
        };
        _ = CodeIndex.Refresh(url, read.Token);
        _ = ContextIndex.Refresh(url, read.Token);
    }

    /// <summary>Cancels the cards' read in flight (the page they are on was left, or a newer read starts).</summary>
    internal void StopCards()
    {
        var stop = _stopCards;
        _stopCards = null;
        stop?.Invoke();
    }

    private CodeIndexCardViewModel WatchCodeIndex(CodeIndexCardViewModel card)
    {
        card.PropertyChanged += CodeIndexChanged;
        return card;
    }

    private ContextIndexCardViewModel WatchContextIndex(ContextIndexCardViewModel card)
    {
        card.PropertyChanged += ContextIndexChanged;
        return card;
    }

    // Either word: the file is there and the profile is not in force (#3622 split the card word; the pill and headline treat both alike).
    private bool IsProfileUnreadable => string.Equals(ProfileState, "unreadable", StringComparison.Ordinal) || string.Equals(ProfileState, "invalid", StringComparison.Ordinal);

    // What the cards say needs a look that the server's one health word does not: the Code index, the Context index and the services the scheduler missed; empty when it is fine.
    private string NeedsLook()
    {
        List<string> parts = [];
        if (string.Equals(CodeIndex.Word, Labels.CodeIndexNeedsLook, StringComparison.Ordinal))
        {
            parts.Add(Labels.CodeIndexLookName);
        }

        if (string.Equals(ContextIndex.Word, Labels.CodeIndexNeedsLook, StringComparison.Ordinal))
        {
            parts.Add(Labels.ContextIndexLookName);
        }

        if (IsProfileUnreadable)
        {
            parts.Add(Labels.ProfileLookName);
        }

        var overdue = _serviceRows.Values.Where(r => r.IsOverdue).Select(r => r.Name).Order(StringComparer.Ordinal).ToList();
        if (overdue.Count > 0)
        {
            parts.Add(Labels.ServicesOverdue(overdue));
        }

        return string.Join(", ", parts);
    }

    // The Code index card read again: the headline follows it.
    private void CodeIndexChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CodeIndexCardViewModel.Word), StringComparison.Ordinal))
        {
            lock (_gate)
            {
                ShowSystemState();
            }
        }
    }

    // The Context index card read again: the headline follows it.
    private void ContextIndexChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ContextIndexCardViewModel.Word), StringComparison.Ordinal))
        {
            lock (_gate)
            {
                ShowSystemState();
            }
        }
    }

    private static string Count(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static List<HealthCell> ViewsCells(AsOfView views)
    {
        List<HealthCell> cells = [new(Labels.ViewsReadUpTo, Labels.ViewsProgress(views.Cursor, views.LedgerHead))];
        if (views.ProjectedAt is { } at)
        {
            cells.Add(new(Labels.ViewsUpdated, StatusText.Time(at)));
        }

        return cells;
    }
}
