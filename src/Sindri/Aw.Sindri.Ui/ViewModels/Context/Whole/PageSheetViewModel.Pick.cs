using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>"Pick another" (<see cref="PageSheetViewModel"/>, continued, pages spec §3.6): a search over the map's pages that repoints the page's map surface.</summary>
internal sealed partial class PageSheetViewModel
{
    /// <summary>How long the typing rests before the search reads.</summary>
    internal static readonly TimeSpan PickDebounce = TimeSpan.FromMilliseconds(250);

    /// <summary>The most rows the search shows.</summary>
    internal const int PickLimit = 50;

    /// <summary>Gets the Pick another button's words.</summary>
    public string PickAnotherText { get; } = Labels.PageSheetPickAnother;

    /// <summary>Gets the search box's name for a screen reader.</summary>
    public string PickBoxName { get; } = Labels.PageSheetPickBoxName;

    /// <summary>Gets the button that closes the search.</summary>
    public string PickBackText { get; } = Labels.PageSheetPickBack;

    /// <summary>Gets a value indicating whether the search shows.</summary>
    public bool IsPicking { get; private set; }

    /// <summary>Gets or sets the search text; the search reads once the typing rests.</summary>
    public string PickQuery { get; set; } = string.Empty;

    /// <summary>Gets the latest search: it ends once its answer is drawn.</summary>
    internal Task Picking { get; private set; } = Task.CompletedTask;

    /// <summary>Gets the pages the search found.</summary>
    public ObservableCollection<PageMatch> PickRows { get; } = [];

    /// <summary>Gets the search's line: "Searching…", the empty answer or the failure; empty while rows show.</summary>
    public string PickStatus { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the search has a line.</summary>
    public bool HasPickStatus => PickStatus.Length > 0;

    /// <summary>Opens the search and reads the first rows.</summary>
    internal void OpenPick() => throw new NotSupportedException(_clock.ToString());

    /// <summary>Closes the search without a pick.</summary>
    internal void ClosePick() => throw new NotSupportedException();

    /// <summary>Points the page at a found page's map surface and closes the search.</summary>
    /// <param name="match">The row of <see cref="PickRows"/>.</param>
    internal void Choose(PageMatch match) => throw new NotSupportedException(match?.Key);
}
