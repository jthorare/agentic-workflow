namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Drilling into a band of a concept's map (<see cref="WholeMapViewModel"/>, continued, #1289): a click on a band opens it, links to the other bands first, with a switch to the band's own links.</summary>
internal sealed partial class WholeMapViewModel
{
    private const string BandPath = "band";

    private WholeBand? _drilled;
    private bool _drillOwn;
    private (WholeMapGeometry Source, WholeBand Band, bool Own, WholeMapGeometry Shown)? _drillCut;

    /// <summary>Gets the buttons over the map, one per drawn band, top to bottom.</summary>
    public IReadOnlyList<BandButton> BandButtons => Geometry is { ByArea: false } geometry && HasConcept
        ? [.. geometry.Bands.OrderBy(b => b.Top).Select(b => new BandButton(b.Band, NodeBands.Title(b.Band), Labels.ContextDrillOpen(NodeBands.Title(b.Band))))]
        : [];

    /// <summary>Gets a value indicating whether the band buttons show: inside a concept, not drilled.</summary>
    public bool ShowBandButtons => !IsDrilled && BandButtons.Count > 0;

    /// <summary>Gets a value indicating whether a band is drilled into.</summary>
    public bool IsDrilled => _drilled is not null;

    /// <summary>Gets the words of the cross segment.</summary>
    public string CrossText { get; } = Labels.ContextDrillCross;

    /// <summary>Gets the words of the own segment: "Links inside Why"; empty when no band is drilled.</summary>
    public string OwnText => _drilled is { } band ? Labels.ContextDrillOwn(NodeBands.Title(band)) : string.Empty;

    /// <summary>Gets or sets a value indicating whether links to the other bands are drawn.</summary>
    public bool ShowCross
    {
        get => !_drillOwn;
        set
        {
            if (value && IsDrilled)
            {
                SetDrillOwn(own: false);
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the band's own links are drawn.</summary>
    public bool ShowOwn
    {
        get => _drillOwn;
        set
        {
            if (value && IsDrilled)
            {
                SetDrillOwn(own: true);
            }
        }
    }

    /// <summary>Gets the line that says the drilled view has no links; empty when it has some.</summary>
    public string DrillEmptyText => !ShowDrillEmpty || _drilled is not { } band ? string.Empty
        : _drillOwn ? Labels.ContextDrillNoOwn(NodeBands.Title(band)) : Labels.ContextDrillNoCross(NodeBands.Title(band));

    /// <summary>Gets a value indicating whether <see cref="DrillEmptyText"/> shows.</summary>
    public bool ShowDrillEmpty => IsDrilled && Shown is { Edges.Count: 0 };

    /// <summary>Drills into a band: links to the other bands first.</summary>
    /// <param name="band">The band.</param>
    internal void DrillInto(WholeBand band)
    {
        if (!HasConcept || Geometry is not { ByArea: false } geometry || !geometry.Bands.Any(b => b.Band == band))
        {
            return;
        }

        (Hovered, Pinned) = (null, null);
        _ = Panel.Close();
        (_drilled, _drillOwn) = (band, false);
        DrillChanged();
    }

    /// <summary>Leaves the drill: the concept's whole map again.</summary>
    /// <returns>True when a band was drilled into.</returns>
    internal bool ExitDrill()
    {
        if (!IsDrilled)
        {
            return false;
        }

        (_drilled, _drillOwn, _drillCut) = (null, false, null);
        DrillChanged();
        return true;
    }

    /// <summary>A click at a point of the world: opens the band whose title is there.</summary>
    /// <param name="x">World x.</param>
    /// <param name="y">World y.</param>
    /// <returns>True when a lane title was hit.</returns>
    internal bool LaneTitleClicked(double x, double y)
    {
        if (!ShowBandButtons || Geometry is not { } geometry)
        {
            return false;
        }

        foreach (var band in geometry.Bands)
        {
            var width = (WholeMapLayout.Margin / 2) + (12 * NodeBands.Title(band.Band).Length) + 12;
            if (x >= 0 && x <= width && y >= band.LaneTop && y < band.Top)
            {
                DrillInto(band.Band);
                return true;
            }
        }

        return false;
    }

    // The geometry cut to the drilled band and mode, drawn instead of the whole (or searched) map.
    private WholeMapGeometry? Drilled(WholeMapGeometry? source)
    {
        if (source is null || _drilled is not { } band)
        {
            return source;
        }

        if (_drillCut is not { } cut || !ReferenceEquals(cut.Source, source) || cut.Band != band || cut.Own != _drillOwn)
        {
            _drillCut = cut = (source, band, _drillOwn, _drillOwn ? WholeMapBandDrill.Own(source, band) : WholeMapBandDrill.Cross(source, band));
        }

        return cut.Shown;
    }

    private void SetDrillOwn(bool own)
    {
        if (_drillOwn != own)
        {
            _drillOwn = own;
            DrillChanged();
        }
    }

    // What the view reads of the drill changed: the drawn set, the buttons and segments, the crumbs and the canvas name; the camera fits what is drawn.
    private void DrillChanged()
    {
        Refocus();
        Changed();
        foreach (var name in (string[])[nameof(Shown), nameof(BandButtons), nameof(ShowBandButtons), nameof(IsDrilled), nameof(OwnText), nameof(ShowCross), nameof(ShowOwn), nameof(ShowDrillEmpty), nameof(DrillEmptyText)])
        {
            OnChanged(name);
        }

        FitRequested?.Invoke(this, EventArgs.Empty);
    }
}
