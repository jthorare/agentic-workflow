using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>The Home page as rows (home spec §7.1, U1): the tabs with their counts, and the groups of the selected split in the order the API ranked them.</summary>
[SuppressMessage("Design", "MA0182", Justification = "The page commit of this PR binds it (#3733, U1).")]
internal sealed class HomeRowsViewModel : ObservableObject
{
    /// <summary>The fact columns every row shows.</summary>
    public static IReadOnlyList<RowColumn> Columns { get; } = [new(HomeRowItem.Why, string.Empty), new(HomeRowItem.Object, string.Empty), new(HomeRowItem.From, string.Empty, Wide: true)];

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
    internal void Show(HomeView page, DateTimeOffset now) => throw new NotSupportedException();

    /// <summary>Adds a later page; a row the page already holds is not added twice.</summary>
    /// <param name="page">The page read with the cursor of the one before.</param>
    /// <param name="now">The time the ages count to.</param>
    internal void Add(HomeView page, DateTimeOffset now) => throw new NotSupportedException();

    /// <summary>Selects a split: its rows show, the counts stay.</summary>
    /// <param name="split">The split to show.</param>
    internal void Select(HomeSplit split) => throw new NotSupportedException();
}
