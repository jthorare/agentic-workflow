using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>The Home page as rows (home spec §7.1, U1): the tabs with their counts, and the groups of the selected split in the order the API ranked them.</summary>
internal sealed class HomeRowsViewModel : ObservableObject
{
    private readonly List<HomeRowItem> _rows = [];

    /// <summary>The fact columns every row shows.</summary>
    public static IReadOnlyList<RowColumn> Columns { get; } = [new(HomeRowItem.Why, string.Empty), new(HomeRowItem.Object, string.Empty), new(HomeRowItem.From, string.Empty, Wide: true)];

    /// <summary>What a screen reader says for the tabs as a whole.</summary>
    public static string TabsName => Labels.HomeTabsName;

    /// <summary>The page's title.</summary>
    public static string Title => Labels.HomeTitle;

    /// <summary>The header's tabs, each with its open count.</summary>
    public IReadOnlyList<HomeTab> Tabs
    {
        get;
        private set => Set(ref field, value);
    } = [];

    /// <summary>The groups that hold a row under the selected split.</summary>
    public IReadOnlyList<HomeRowGroup> Groups
    {
        get;
        private set => Set(ref field, value);
    } = [];

    /// <summary>The one plain line a split with no rows shows, or null while it has rows.</summary>
    public string? EmptyLine
    {
        get;
        private set => Set(ref field, value);
    }

    /// <summary>The selected split.</summary>
    public HomeSplit Split
    {
        get;
        private set => Set(ref field, value);
    }

    /// <summary>Shows a first page, in place of what the page held.</summary>
    /// <param name="page">The read's first page.</param>
    /// <param name="now">The time the ages count to.</param>
    internal void Show(HomeView page, DateTimeOffset now)
    {
        _rows.Clear();
        Add(page, now);
    }

    /// <summary>Adds a later page; a row the page already holds is not added twice.</summary>
    /// <param name="page">The page read with the cursor of the one before.</param>
    /// <param name="now">The time the ages count to.</param>
    internal void Add(HomeView page, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(page);
        var held = _rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        _rows.AddRange(page.NeedsYou.Concat(page.Moving).Where(r => held.Add(r.Id)).Select(r => HomeRowItem.Of(r, now)));
        Fill();
    }

    /// <summary>Selects a split: its rows show, the counts stay.</summary>
    /// <param name="split">The split to show.</param>
    internal void Select(HomeSplit split)
    {
        Split = split;
        Fill();
    }

    private static bool IsOpen(HomeRowItem row) => string.Equals(row.Row.State, "open", StringComparison.Ordinal);

    private void Fill()
    {
        var open = _rows.Where(IsOpen).ToList();
        Tabs = [.. HomeSplits.InOrder.Select(s => new HomeTab(s, HomeSplits.Label(s), open.Count(r => HomeSplits.Holds(s, r.Row.Kind)), s == Split))];
        var shown = open.Where(r => HomeSplits.Holds(Split, r.Row.Kind)).ToList();
        // An empty tab keeps the Needs you head with its 0 (#3733 frame tab-messages-empty): the tab's one line sits under it.
        Groups = shown.Count == 0
            ? [new HomeRowGroup(Labels.HomeNeedsYouGroup, [])]
            : [.. new[] { Group(Labels.HomeNeedsYouGroup, shown, "needs-you"), Group(Labels.HomeMovingGroup, shown, "moving") }.OfType<HomeRowGroup>()];
        EmptyLine = shown.Count > 0 ? null : HomeSplits.EmptyLine(Split);
    }

    // On a tab that holds rows, a group with no row under the split is not listed.
    private static HomeRowGroup? Group(string heading, List<HomeRowItem> shown, string group)
    {
        var rows = shown.Where(r => string.Equals(r.Row.Group, group, StringComparison.Ordinal)).ToList();
        return rows.Count == 0 ? null : new HomeRowGroup(heading, rows);
    }
}
