using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The stage of an opened area (<see cref="WholeMapViewModel"/>, continued; #3674): the ranked list from the answer first, the canvas after its layout when the page has at most 40 nodes, and paging.</summary>
internal sealed partial class WholeMapViewModel
{
    private int _layoutRun;

    /// <summary>Gets or sets the window width in px, which decides whether Core draws its three groups side by side (the view sets it).</summary>
    internal double ViewWidth { get; set; } = WholeMapLayout.DefaultWidth;

    /// <summary>Raised when another page of the open area is wanted (the new page, 1-based).</summary>
    internal event EventHandler<ValueEventArgs<int>>? AreaPageRequested;

    private int _inView;
    private int _below;

    /// <summary>Gets whether the bar over an opened area's canvas shows.</summary>
    public bool ShowAreaBar => IsAreaOpen && Geometry is { IsAreaCanvas: true };

    /// <summary>Gets the bar's words: "40 nodes · 38 in view".</summary>
    public string AreaBarText => Geometry is { IsAreaCanvas: true } g ? Labels.ContextAreaBar(g.Nodes.Count, _inView) : string.Empty;

    /// <summary>Gets whether nodes lie below the screen, so the fade at the foot shows.</summary>
    public bool ShowMoreBelow => ShowAreaBar && _below > 0;

    /// <summary>Gets the fade's words: "2 more below".</summary>
    public string MoreBelowText => Labels.ContextAreaMoreBelow(_below);

    /// <summary>The part of the world on screen changed: counts the nodes in it and below it.</summary>
    /// <param name="view">The part of the world on screen.</param>
    internal void ViewportChanged(WholeRect view)
    {
        if (Geometry is not { IsAreaCanvas: true } geometry)
        {
            return;
        }

        var (inView, below) = (geometry.Nodes.Count(n => n.Rect.Intersects(view)), geometry.Nodes.Count(n => n.Rect.Y >= view.Bottom));
        if (inView == _inView && below == _below)
        {
            return;
        }

        (_inView, _below) = (inView, below);
        OnChanged(nameof(AreaBarText));
        OnChanged(nameof(ShowMoreBelow));
        OnChanged(nameof(MoreBelowText));
    }

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
        var placed = await Task.Run(() => WholeMapLayout.Place(answer, ViewWidth)).ConfigureAwait(true);
        if (run == _layoutRun && IsAreaOpen)
        {
            Geometry = placed;
            _inView = placed.Nodes.Count;   // until the view reports what is on screen
            _below = 0;
            Changed();
        }
    }
}
