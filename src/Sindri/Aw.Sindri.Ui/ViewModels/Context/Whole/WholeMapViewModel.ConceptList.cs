using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>An opened concept's member list (<see cref="WholeMapViewModel"/>, continued; #3674): tabs with the counts the card shows, and the ranked list, 50 a page, through the same list the area stage uses.</summary>
internal sealed partial class WholeMapViewModel
{
    private static readonly string[] _tabKeys = ["all", "code", "feature", "record"];

    /// <summary>Raised when another tab or page of the concept's list is wanted (the tab and the page, 1-based).</summary>
    internal event EventHandler<ValueEventArgs<(string Tab, int Page)>>? ConceptListRequested;

    /// <summary>The tab of the concept's list on screen: all, code, feature or record.</summary>
    internal string ConceptTab { get; private set; } = "all";

    /// <summary>The page of the concept's list on screen (1-based).</summary>
    internal int ConceptPage { get; private set; } = 1;

    /// <summary>Gets whether the answer on the stage is a concept's member list.</summary>
    public bool IsConceptList => HasConcept && Answer?.ConceptList is not null;

    /// <summary>Gets the accessible name of the row of tabs.</summary>
    public static string ConceptTabsName => Labels.ContextWholeConceptTabs;

    /// <summary>Gets the line for a concept that holds nothing.</summary>
    public static string ConceptEmptyText => Labels.ContextWholeConceptEmpty;

    /// <summary>Gets whether the empty concept's line shows.</summary>
    public bool ShowConceptEmpty => Answer?.ConceptList is { } list && HasConcept && TotalOf(list) == 0;

    /// <summary>Gets the tabs, All first, each with the count it holds; empty when the stage is not a concept's list.</summary>
    public IReadOnlyList<ConceptTabItem> ConceptTabs => HasConcept && Answer?.ConceptList is { } list
        ? [.. _tabKeys.Select(key => TabOf(key, list))]
        : [];

    // The card's count: the code its features are implemented by, the features and the records.
    private static int TotalOf(WholeConceptList list) => list.Code + list.Features + list.Records;

    private static ConceptTabItem TabOf(string key, WholeConceptList list)
    {
        var count = key switch { "code" => list.Code, "feature" => list.Features, "record" => list.Records, _ => TotalOf(list) };
        return new ConceptTabItem(key, Labels.ContextWholeConceptTab(key, count), string.Equals(key, list.Tab, StringComparison.Ordinal), CanChoose: count > 0);
    }

    /// <summary>A tab was pressed: the list of that tab, from its first page. A tab that holds nothing, or the one on screen, asks for nothing.</summary>
    /// <param name="key">all, code, feature or record.</param>
    internal void ChooseTab(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ConceptTabs.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal)) is { CanChoose: true, IsSelected: false })
        {
            ConceptListRequested?.Invoke(this, new ValueEventArgs<(string, int)>((key, 1)));
        }
    }

    // A concept's member list on the stage: the list from the answer, no canvas and no edges.
    private void ShowConceptList(WholeMapView answer, WholeConceptList list)
    {
        (_overview, _beforeZoom, IsAreaOpen, AreaTitle) = (null, null, false, string.Empty);
        (HitKeys, IsZoomed, MoreTouchedText, _zoomSet) = (new HashSet<string>(StringComparer.Ordinal), false, string.Empty, null);
        (SearchActive, NoMatches, _searchSet) = (false, false, null);
        _layoutRun++;   // a layout still running is for an area that is gone
        Answer = answer;
        (ListLoading, IsListFailed) = (false, false);
        Geometry = null;
        (ConceptTab, ConceptPage) = (list.Tab, list.Page);
        (AreaKey, AreaPage, AreaPages, AreaLaidOut) = (string.Empty, list.Page, list.Pages, Task.CompletedTask);
        ByAreaNote = string.Empty;
        ForgetHighlight();
        Head = Labels.ContextWholeHead(answer.MapVersion);
        SizeText = Labels.ContextWholeSize(answer.Counts.Nodes, answer.Counts.Edges, TimeZoneInfo.ConvertTime(answer.AsOf, _zone()));
        Areas = [];
        (IsReady, IsLoading, IsFailed) = (true, false, false);
        PagesFollow(answer);
        Changed();
    }
}
