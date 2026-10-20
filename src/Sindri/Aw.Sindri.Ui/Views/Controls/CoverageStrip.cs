using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

/// <summary>
/// The coverage strip of the context map's control row (F3 spec 1 §2, "1d"): one 34 by 6 bar per area (full in Ink, a limited
/// area hatched, an unavailable one empty) and a sentence, as one button; pressing it opens a flyout with a 36 px row per area.
/// The words are the owner's: the strip draws what it is given.
/// </summary>
internal sealed class CoverageStrip : Button
{
    /// <summary>The areas, one bar each.</summary>
    internal static readonly StyledProperty<IReadOnlyList<CoverageArea>> AreasProperty =
        AvaloniaProperty.Register<CoverageStrip, IReadOnlyList<CoverageArea>>(nameof(Areas), []);

    /// <summary>The sentence's bold lead ("3 of 4 areas searched.").</summary>
    internal static readonly StyledProperty<string> LeadProperty = AvaloniaProperty.Register<CoverageStrip, string>(nameof(Lead), "");

    /// <summary>The rest of the sentence ("Code is limited.").</summary>
    internal static readonly StyledProperty<string> RestProperty = AvaloniaProperty.Register<CoverageStrip, string>(nameof(Rest), "");

    private const double BarWidth = 34;
    private const double BarHeight = 6;
    private const double RowHeight = 36;
    private const double HatchTile = 6;

    private readonly StackPanel _bars = new() { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _lead = new() { FontWeight = FontWeight.Medium };
    private readonly TextBlock _rest = new();
    private readonly StackPanel _rows = new() { MinWidth = 280 };

    static CoverageStrip()
    {
        _ = AreasProperty.Changed.AddClassHandler<CoverageStrip>((s, _) => s.Rebuild());
        _ = LeadProperty.Changed.AddClassHandler<CoverageStrip>((s, _) => s.Rebuild());
        _ = RestProperty.Changed.AddClassHandler<CoverageStrip>((s, _) => s.Rebuild());
    }

    /// <summary>Initializes a new instance of the <see cref="CoverageStrip"/> class.</summary>
    public CoverageStrip()
    {
        Classes.Add("coverage-strip");
        _lead.Classes.Add("caption");
        _lead.Classes.Add("lead");
        _rest.Classes.Add("caption");
        var words = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { _lead, _rest } };
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _bars, words } };
        ActualThemeVariantChanged += (_, _) => Rebuild();   // the bar brushes were resolved for the old variant
        Flyout = new Flyout { Content = _rows, Placement = PlacementMode.BottomEdgeAlignedRight };
    }

    /// <summary>Gets or sets the areas.</summary>
    internal IReadOnlyList<CoverageArea> Areas
    {
        get => GetValue(AreasProperty);
        set => SetValue(AreasProperty, value);
    }

    /// <summary>Gets or sets the sentence's lead.</summary>
    internal string Lead
    {
        get => GetValue(LeadProperty);
        set => SetValue(LeadProperty, value);
    }

    /// <summary>Gets or sets the rest of the sentence.</summary>
    internal string Rest
    {
        get => GetValue(RestProperty);
        set => SetValue(RestProperty, value);
    }

    /// <summary>The panel the flyout shows (its rows), for tests: a closed flyout's content is not in the visual tree.</summary>
    internal Panel FlyoutRows => _rows;

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        _lead.Text = Lead;
        _rest.Text = Rest;
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Rest) ? Lead : Lead + " " + Rest);
        _bars.Children.Clear();
        _rows.Children.Clear();
        foreach (var area in Areas)
        {
            _bars.Children.Add(Bar(area.Level));
            _rows.Children.Add(Row(area));
        }
    }

    private Border Bar(CoverageLevel level) => new()
    {
        Width = BarWidth,
        Height = BarHeight,
        CornerRadius = Resource<CornerRadius>("Radius.XS", new(4)),
        Background = level switch
        {
            CoverageLevel.Searched => Brush("Brush.Ink"),
            CoverageLevel.Limited => Hatched(),
            CoverageLevel.NotAvailable => Brush("Brush.Surface3"),
            _ => Brush("Brush.Surface3"),
        },
    };

    private Grid Row(CoverageArea area)
    {
        var name = new TextBlock { Text = area.Name, VerticalAlignment = VerticalAlignment.Center };
        name.Classes.Add("body");
        var level = new TextBlock { Text = area.LevelWord, VerticalAlignment = VerticalAlignment.Center };
        level.Classes.Add("caption");
        level.Foreground = Brush("Brush.InkSubtle");
        var row = new Grid { Height = RowHeight, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(Bar(area.Level));
        row.Children.Add(name);
        row.Children.Add(level);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(level, 2);
        AutomationProperties.SetName(row, area.Detail is null ? $"{area.Name}: {area.LevelWord}" : $"{area.Name}: {area.LevelWord}. {area.Detail}");
        if (area.Detail is { } detail)
        {
            ToolTip.SetTip(row, detail);
        }

        return row;
    }

    // Diagonal stripes of InkSubtle over Surface3, 6 px to the tile: the limited bar.
    private DrawingBrush Hatched()
    {
        var tile = new Rect(0, 0, HatchTile, HatchTile);
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = Brush("Brush.Surface3"), Geometry = new RectangleGeometry(tile) },
                new GeometryDrawing
                {
                    Pen = new Pen(Brush("Brush.InkSubtle"), 3),
                    Geometry = new LineGeometry(new Point(-1, HatchTile + 1), new Point(HatchTile + 1, -1)),
                },
            },
            ClipGeometry = new RectangleGeometry(tile),
        };
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(tile, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(tile, RelativeUnit.Absolute),
        };
    }

    private IBrush Brush(string key) => Resource<IBrush>(key, Brushes.Gray);

    private T Resource<T>(string key, T fallback) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is T found ? found : fallback;
}

#pragma warning restore MA0182
