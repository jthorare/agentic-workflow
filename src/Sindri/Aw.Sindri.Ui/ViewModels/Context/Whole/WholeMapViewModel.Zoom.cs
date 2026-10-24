using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's zoom onto a query's answer (<see cref="WholeMapViewModel"/>, continued; whole-map spec §3.1 "Zoomed in").</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>The nodes a query's answer holds, drawn at full strength while the rest of the map stays behind them; empty when nothing is zoomed to.</summary>
    internal IReadOnlySet<string> HitKeys { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Whether the map read is the overview of features, which a query reaches through "locate". It stays so while zoomed, when the drawn geometry holds nodes.</summary>
    internal bool AnswerByArea => Answer is { } answer && string.Equals(answer.Detail, "areas", StringComparison.Ordinal);

    /// <summary>Whether the camera is on a query's answer.</summary>
    public bool IsZoomed { get; private set; }

    /// <summary>The "Whole map" button's words.</summary>
    public string BackText { get; } = Labels.ContextWholeBack;

    /// <summary>The card's title.</summary>
    public string CardTitle { get; } = Labels.ContextMapWhyTitle;

    /// <summary>The card's words: pick a starting point until one is chosen, then what the map says about it.</summary>
    public string CardText => IsZoomed ? Labels.ContextMapWhyNone : Labels.ContextWholePick;

    /// <summary>"2 more features touched" once a query touched more features than were opened; empty otherwise.</summary>
    public string MoreTouchedText { get; private set; } = string.Empty;

    /// <summary>Whether the more-touched chip shows.</summary>
    public bool HasMoreTouched => MoreTouchedText.Length > 0;

    private (WholeMapView? Answer, WholeMapGeometry? Geometry, IReadOnlyList<WholeAreaTileViewModel> Areas)? _beforeZoom;

    /// <summary>
    /// The camera goes to a query's answer: the nodes of the answer that are on this map are drawn at full strength and the camera eases to the box around them.
    /// The rest of the map stays where it is, dimmed.
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
        Changed();
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
        var byKey = overview.Areas.ToDictionary(a => a.Key, StringComparer.Ordinal);
        Areas = [.. geometry.Order.Select((key, i) => new WholeAreaTileViewModel(byKey[key], i, OpenArea))];
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

        (HitKeys, IsZoomed, MoreTouchedText) = (new HashSet<string>(StringComparer.Ordinal), false, string.Empty);
        Changed();
        ResetSearch();
        FitRequested?.Invoke(this, EventArgs.Empty);
    }
}
