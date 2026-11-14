using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The stage of an opened area (<see cref="WholeMapViewModel"/>, continued; #3674): the ranked list from the answer first, the canvas after its layout when the page has at most 40 nodes, and paging.</summary>
internal sealed partial class WholeMapViewModel
{
    private int _layoutRun;

    /// <summary>Raised when another page of the open area is wanted (the new page, 1-based).</summary>
    internal event EventHandler<ValueEventArgs<int>>? AreaPageRequested;

    /// <summary>Gets the accessible name of the ranked list.</summary>
    public static string AreaListName => Labels.ContextWholeAreaList;

    /// <summary>Gets the words of the previous-page button.</summary>
    public static string PreviousPageText => Labels.ContextWholePreviousPage;

    /// <summary>Gets the words of the next-page button.</summary>
    public static string NextPageText => Labels.ContextWholeNextPage;

    /// <summary>Gets whether the stage is the ranked list: an area is open and its page holds more nodes than the canvas draws.</summary>
    public bool ShowAreaList => IsAreaOpen && Stage == WholeStage.List;

    /// <summary>Gets whether the open area has more than one page.</summary>
    public bool HasAreaPages => IsAreaOpen && AreaPages > 1;

    /// <summary>Gets the paging line: "Page 2 of 3 · 50 of 121 items".</summary>
    public string AreaPageText => HasAreaPages ? Labels.ContextWholeAreaPage(AreaPage, AreaPages, Answer?.Nodes?.Count ?? 0, Answer?.Area?.Counts.Nodes ?? 0) : string.Empty;

    /// <summary>Gets whether a page comes before this one.</summary>
    public bool CanPreviousPage => AreaPage > 1;

    /// <summary>Gets whether a page comes after this one.</summary>
    public bool CanNextPage => AreaPage < AreaPages;

    /// <summary>The previous page was asked for.</summary>
    internal void PreviousPage()
    {
        if (CanPreviousPage)
        {
            AreaPageRequested?.Invoke(this, new ValueEventArgs<int>(AreaPage - 1));
        }
    }

    /// <summary>The next page was asked for.</summary>
    internal void NextPage()
    {
        if (CanNextPage)
        {
            AreaPageRequested?.Invoke(this, new ValueEventArgs<int>(AreaPage + 1));
        }
    }

    // Places the answer on a pool thread and draws it when it is back, unless the area was closed or another page was read meanwhile.
    private async Task LayOutArea(WholeMapView answer)
    {
        var run = ++_layoutRun;
        var placed = await Task.Run(() => WholeMapLayout.Place(answer)).ConfigureAwait(true);
        if (run == _layoutRun && IsAreaOpen)
        {
            Geometry = placed;
            Changed();
        }
    }
}
