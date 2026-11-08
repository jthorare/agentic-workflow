namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page's width, no-match and empty actions (<see cref="AgentsPageViewModel"/>, continued; agents spec §2, §3).</summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>True under the page width where only Status, Agent and For stay.</summary>
    public bool IsCompact { get; private set => Set(ref field, value); }

    /// <summary>True when every column shows.</summary>
    public bool IsWide => !IsCompact;

    /// <summary>"Clear filters": the button on the no-match line.</summary>
    public string ClearFiltersText { get; } = Labels.ClearFilters;

    /// <summary>True while a filter is set.</summary>
    public bool IsFiltered => ProviderFilter is not null || StateFilter is not null || RepoFilter is not null;

    /// <summary>True when the list has sessions and the filters leave none of them: the line offers to clear the filters.</summary>
    public bool ShowsClearFilters => IsEmpty && IsFiltered && _list is { Sessions.Count: > 0 };

    /// <summary>What resumes a recently ended session, for its sheet.</summary>
    /// <param name="reference">The session's ref.</param>
    /// <returns>Null when the ref is not a recently ended session; empty when it has no resume command; else the command.</returns>
    internal string? ResumeOf(string reference) => _ended.TryGetValue(reference, out var ended) ? ended.ResumeCommand : null;

    /// <summary>The page's width changed: under 640 px the table keeps three columns.</summary>
    /// <param name="width">The page's width in px.</param>
    internal void Resize(double width)
    {
        var compact = width < Labels.AgentsWideFrom;
        if (compact == IsCompact)
        {
            return;
        }

        IsCompact = compact;
        OnChanged(nameof(IsWide));
        foreach (var row in _rows.Values)
        {
            row.SetCompact(compact);
        }
    }

    /// <summary>Drops every filter and draws the list again.</summary>
    internal void ClearFilters()
    {
        (ProviderFilter, StateFilter, RepoFilter) = (null, null, null);
        OnChanged(nameof(ProviderFilterName));
        OnChanged(nameof(StateFilterName));
        OnChanged(nameof(RepoFilterName));
        Redraw();
    }
}
