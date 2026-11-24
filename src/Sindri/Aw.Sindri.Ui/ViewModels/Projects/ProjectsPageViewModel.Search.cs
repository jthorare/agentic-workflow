using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The Projects page's search (<see cref="ProjectsPageViewModel"/>, continued; issue 3717 frames round 13 search, search-none): `/` puts focus in the field,
/// the rows of the view shown keep only projects whose name, note or lead holds the words (any case), the field counts "2 of 32", a row found only by its
/// note says so, and Esc clears it. It filters what the server served; it reads nothing.
/// </summary>
internal sealed partial class ProjectsPageViewModel
{
    private bool _searchFocused;

    /// <summary>`/` asked for the search field: the view focuses it.</summary>
    internal event EventHandler? SearchRequested;

    /// <summary>What is typed in the search field. Public set: the field writes it back (TwoWay).</summary>
    public string SearchText
    {
        get;
        set
        {
            var text = value ?? string.Empty;
            if (string.Equals(field, text, StringComparison.Ordinal))
            {
                return;
            }

            Set(ref field, text);
            OnChanged(nameof(IsSearching));
            Arrange();
        }
    } = string.Empty;

    /// <summary>True while the field holds words.</summary>
    public bool IsSearching => Query.Length > 0;

    /// <summary>"2 of 32": the rows found of the rows the view holds; empty while not searching.</summary>
    public string SearchCount { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the search found no project in the view: its own empty state.</summary>
    public bool IsSearchEmpty { get; private set => Set(ref field, value); }

    /// <summary>"No project matches “xyz”".</summary>
    public string SearchEmptyTitle { get; private set => Set(ref field, value); } = string.Empty;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The field's placeholder.</summary>
    public string SearchPlaceholder => Labels.ProjectsSearchPlaceholder;

    /// <summary>The field's accessible name.</summary>
    public string SearchName => Labels.ProjectsSearchName;

    /// <summary>What search looks at, under the no-match title.</summary>
    public string SearchEmptyBody => Labels.ProjectsSearchNoneBody;

    /// <summary>The no-match state's button.</summary>
    public string ClearSearchText => Labels.ProjectsClearSearch;

    /// <summary>The key on the no-match button.</summary>
    public string ClearSearchKey => Labels.ProjectsClearSearchKey;
#pragma warning restore CA1822, MA0041

    private string Query => SearchText.Trim();

    /// <summary>Esc in the field, or Clear search: empties the field.</summary>
    /// <returns>True when there was text to clear.</returns>
    internal bool ClearSearch()
    {
        if (SearchText.Length == 0)
        {
            return false;
        }

        SearchText = string.Empty;
        return true;
    }

    /// <summary>The field got or lost focus: the footer shows the field's keys (Esc clear, ↓ to the list, Enter open) while it has it.</summary>
    /// <param name="focused">True while the field has focus.</param>
    internal void SearchFocused(bool focused)
    {
        _searchFocused = focused;
        ShowKeyHints();
    }

    private bool AskedForSearch()
    {
        SearchRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // The projects the search keeps: all while the field is empty.
    private IReadOnlyList<ProjectCardView> Searched(IReadOnlyList<ProjectCardView> cards) =>
        IsSearching ? [.. cards.Where(c => ByName(c) || ByLead(c) || ByNote(c))] : cards;

    private bool ByName(ProjectCardView card) => card.Name.Contains(Query, StringComparison.OrdinalIgnoreCase);

    private bool ByLead(ProjectCardView card) => card.Lead?.Name?.Contains(Query, StringComparison.OrdinalIgnoreCase) == true;

    private bool ByNote(ProjectCardView card) => card.Notes?.Contains(Query, StringComparison.OrdinalIgnoreCase) == true;

    // After a layout: the field's count, the no-match state, and the rows found only by their note, over every row the view can list (open rows and, on Projects, the folds).
    private void ShowSearch(IReadOnlyList<ProjectCardView> listed, int found)
    {
        SearchCount = IsSearching ? Labels.ProjectsSearchCount(found, listed.Count) : string.Empty;
        IsSearchEmpty = IsSearching && found == 0 && View != ProjectView.Triage;
        SearchEmptyTitle = IsSearchEmpty ? Labels.ProjectsSearchNone(Query) : string.Empty;
        foreach (var served in listed)
        {
            if (_cards.TryGetValue(served.Id, out var card))
            {
                card.MatchedInNote = IsSearching && !ByName(served) && !ByLead(served) && ByNote(served);
            }
        }
    }
}
