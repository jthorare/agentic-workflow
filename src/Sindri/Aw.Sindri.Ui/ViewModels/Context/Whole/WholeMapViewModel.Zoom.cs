using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's zoom onto a query's answer (<see cref="WholeMapViewModel"/>, continued; whole-map spec §3.1 "Zoomed in").</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>The nodes a query's answer holds, drawn with their direct links while the rest of the map is not drawn; empty when nothing is zoomed to.</summary>
    internal IReadOnlySet<string> HitKeys { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Whether the map read is the overview of features, which a query reaches through "locate". It stays so while zoomed, when the drawn geometry holds nodes.</summary>
    internal bool AnswerByArea => Answer is { } answer && string.Equals(answer.Detail, "areas", StringComparison.Ordinal);

    /// <summary>Whether the camera is on a query's answer.</summary>
    public bool IsZoomed { get; private set; }

    /// <summary>"2 more features touched" once a query touched more features than were opened; empty otherwise.</summary>
    public string MoreTouchedText { get; private set; } = string.Empty;

    /// <summary>Whether the more-touched chip shows.</summary>
    public bool HasMoreTouched => MoreTouchedText.Length > 0;

    private (WholeMapView? Answer, WholeMapGeometry? Geometry, IReadOnlyList<WholeAreaTileViewModel> Areas)? _beforeZoom;
    private HashSet<string>? _zoomSet;

    /// <summary>
    /// The camera goes to a query's answer: the nodes of the answer that are on this map are drawn at full strength and the camera eases to the box around them.
    /// Only the answer and its direct links are drawn.
    /// </summary>
    /// <param name="keys">The keys of the answer's nodes.</param>
    /// <returns>True when any of them is on the map; false moves nothing.</returns>
    internal bool Zoom(IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (Geometry is not { } geometry)
        {
            return false;
        }

        var hit = geometry.Nodes.Where(n => keys.Contains(n.Key, StringComparer.Ordinal)).ToList();
        if (hit.Count == 0)
        {
            return false;
        }

        HitKeys = hit.Select(n => n.Key).ToHashSet(StringComparer.Ordinal);
        IsZoomed = true;
        _zoomSet = new HashSet<string>(HitKeys.SelectMany(Around), StringComparer.Ordinal);   // the answer and what it links to are drawn; the rest is not
        Changed();
        OnChanged(nameof(Shown));
        OnChanged(nameof(Items));
        ZoomRequested?.Invoke(this, new ValueEventArgs<WholeRect>(WholeZoomBox.Around([.. hit.Select(n => n.Rect)])));
        return true;
    }

    /// <summary>
    /// The camera goes to a query's answer by feature: the features it touches are opened in full (the three with most answer nodes, the rest counted), the other tiles stay tiles.
    /// </summary>
    /// <param name="overview">The overview the answer sits in.</param>
    /// <param name="opened">The opened features' answers.</param>
    /// <param name="keys">The keys of the answer's nodes.</param>
    /// <param name="notOpened">How many touched features were not opened.</param>
    /// <returns>True when any answer node is in an opened feature; false changes nothing.</returns>
    internal bool ZoomIntoAreas(WholeMapView overview, IReadOnlyList<WholeMapView> opened, IReadOnlyCollection<string> keys, int notOpened)
    {
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(keys);
        var geometry = WholeMapLayout.PlaceMixed(overview, opened);
        if (!geometry.Nodes.Any(n => keys.Contains(n.Key, StringComparer.Ordinal)))
        {
            return false;
        }

        _beforeZoom ??= (Answer, Geometry, Areas);
        Geometry = geometry;
        ForgetHighlight();
        Areas = HomedAreas.Tiles(overview.Areas, opened.SelectMany(o => o.Nodes ?? []), geometry.Order, OpenArea);
        MoreTouchedText = notOpened > 0 ? Labels.ContextWholeMoreTouched(notOpened) : string.Empty;
        return Zoom(keys);
    }

    /// <summary>Back to the whole map: the answer's nodes stop standing out, features opened for it close, and the camera fits the world again.</summary>
    internal void Unzoom()
    {
        if (!IsZoomed)
        {
            EndSearch();
            return;
        }

        if (_beforeZoom is { } before)
        {
            (Answer, Geometry, Areas) = before;
            ForgetHighlight();
            _beforeZoom = null;
        }

        (HitKeys, IsZoomed, MoreTouchedText, _zoomSet) = (new HashSet<string>(StringComparer.Ordinal), false, string.Empty, null);
        Changed();
        ResetSearch();
        FitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The scales the stage allows for a map whose whole fits at <paramref name="fit"/>: out to the fit or a quarter (whichever is smaller; never below 2 %), in to <see cref="MaxScale"/>.</summary>
    /// <param name="fit">The scale that shows the whole world.</param>
    /// <returns>The smallest and the largest scale.</returns>
    internal static (double Min, double Max) ZoomLimits(double fit) => (Math.Clamp(Math.Min(fit, FirstOpenFloor), 0.02, FirstOpenFloor), MaxScale);

    /// <summary>Whether Zoom in can act.</summary>
    /// <param name="scale">The zoom now.</param>
    /// <param name="max">The largest scale.</param>
    /// <returns>True below the largest scale.</returns>
    internal static bool CanZoomIn(double scale, double max) => scale < max - 0.001;

    /// <summary>Whether Zoom out can act.</summary>
    /// <param name="scale">The zoom now.</param>
    /// <param name="min">The smallest scale.</param>
    /// <returns>True above the smallest scale.</returns>
    internal static bool CanZoomOut(double scale, double min) => scale > min + 0.001;
}
