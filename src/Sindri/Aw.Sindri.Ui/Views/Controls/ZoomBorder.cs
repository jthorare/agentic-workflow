using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The context map's pannable, zoomable stage (F3 spec 1 §2): it holds the world as its child and moves it with a matrix.
/// A drag on empty canvas pans, the wheel pans, Ctrl or Cmd with the wheel zooms around the cursor, and the scale stays
/// inside <see cref="MinZoom"/> and <see cref="MaxZoom"/> (62 % to 100 % unless the page says otherwise). The step and fit moves
/// animate over Motion.Base, which is zero under reduced motion. A 22 px dotted grid fills the stage behind the world and moves
/// with it.
/// </summary>
internal sealed partial class ZoomBorder : Border
{
    /// <summary>The view: scale and offset of the world.</summary>
    internal static readonly DirectProperty<ZoomBorder, ZoomView> ViewProperty =
        AvaloniaProperty.RegisterDirect<ZoomBorder, ZoomView>(nameof(View), z => z.View);

    /// <summary>The world's rectangle (origin included), which <see cref="Fit"/> fits to the stage.</summary>
    internal static readonly StyledProperty<Rect> WorldBoundsProperty = AvaloniaProperty.Register<ZoomBorder, Rect>(nameof(WorldBounds), new Rect(0, 0, 1360, 872));

    /// <summary>The smallest scale.</summary>
    internal static readonly StyledProperty<double> MinZoomProperty = AvaloniaProperty.Register<ZoomBorder, double>(nameof(MinZoom), ZoomRange.Default.Min);

    /// <summary>The largest scale.</summary>
    internal static readonly StyledProperty<double> MaxZoomProperty = AvaloniaProperty.Register<ZoomBorder, double>(nameof(MaxZoom), ZoomRange.Default.Max);

    private const double WheelStep = 40;
    private const double ZoomStep = 1.2;
    private const double ClickSlop = 3;
    private const double DotPitch = 22;

    private (ZoomView From, ZoomView To, TimeSpan? Start, TimeSpan Length, int Id)? _move;
    private DrawingBrush? _dots;
    private int _moves;

    static ZoomBorder()
    {
        _ = WorldBoundsProperty.Changed.AddClassHandler<ZoomBorder>((z, _) => z.Keep());
        _ = MinZoomProperty.Changed.AddClassHandler<ZoomBorder>((z, _) => z.Keep());
        _ = MaxZoomProperty.Changed.AddClassHandler<ZoomBorder>((z, _) => z.Keep());
    }

    /// <summary>Initializes a new instance of the <see cref="ZoomBorder"/> class.</summary>
    public ZoomBorder()
    {
        ClipToBounds = true;
        Focusable = false;
        Background = Brushes.Transparent;
        AutomationProperties.SetName(this, "Pannable map");
        ActualThemeVariantChanged += (_, _) =>
        {
            _dots = null;   // its dot colour was resolved for the old variant
            Apply(View);
        };
    }

    /// <summary>Raised when the pointer is pressed and released on empty canvas without moving (a click, which closes the inspector).</summary>
    internal event EventHandler? EmptyClicked;

    /// <summary>Gets the current view.</summary>
    internal ZoomView View { get; private set; } = ZoomView.Identity;

    /// <summary>Gets where the view is going: the end of an unfinished transition, else the view itself.</summary>
    internal ZoomView Target => _move?.To ?? View;

    /// <summary>Gets or sets the world's rectangle.</summary>
    internal Rect WorldBounds
    {
        get => GetValue(WorldBoundsProperty);
        set => SetValue(WorldBoundsProperty, value);
    }

    /// <summary>Gets or sets the smallest scale.</summary>
    internal double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    /// <summary>Gets or sets the largest scale.</summary>
    internal double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <summary>Gets a value indicating whether a zoom transition is running.</summary>
    internal bool IsMoving => _move is not null;

    private ZoomRange Range => new(MinZoom, MaxZoom);

    private Point Middle => new(Bounds.Width / 2, Bounds.Height / 2);

    /// <summary>Zooms in one step around the middle of the stage.</summary>
    internal void ZoomIn() => MoveTo(View.ZoomAt(Middle, ZoomStep, Range));

    /// <summary>Zooms out one step around the middle of the stage.</summary>
    internal void ZoomOut() => MoveTo(View.ZoomAt(Middle, 1 / ZoomStep, Range));

    /// <summary>Shows all of the world (clamped to the zoom range).</summary>
    internal void Fit() => FitBounds(WorldBounds);

    /// <summary>Shows a rectangle of the world (clamped to the zoom range), whatever its origin.</summary>
    /// <param name="bounds">The rectangle, in world coordinates.</param>
    internal void FitBounds(Rect bounds) => MoveTo(ZoomView.Fit(Bounds.Size, bounds, Range));

    /// <summary>Pans the least that brings a world rectangle fully into view, clear of the inspector over the right edge.</summary>
    /// <param name="target">The rectangle, in world coordinates.</param>
    /// <param name="rightInset">Width of the panel over the stage's right edge.</param>
    internal void Reveal(Rect target, double rightInset = 0) => MoveTo(View.Reveal(target, Bounds.Size, rightInset));

    /// <summary>Moves to a view (clamped to the zoom range's scale and kept on the stage), animated like the zoom buttons.</summary>
    /// <param name="view">The wanted view.</param>
    internal void MoveToView(ZoomView view) => MoveTo(Kept(view with { Scale = Range.Clamp(view.Scale) }));

    /// <summary>Puts the stage on a view now, with no transition (a drag on the minimap).</summary>
    /// <param name="view">The wanted view.</param>
    internal void SetView(ZoomView view)
    {
        Settle();
        Apply(Kept(view with { Scale = Range.Clamp(view.Scale) }));
    }

    /// <summary>The zoom keys, one entry point for every page that hosts a stage: plus or equals zooms in, minus out, 0 fits.</summary>
    /// <param name="key">The key pressed.</param>
    /// <returns>True when the key was a zoom key and the stage acted on it.</returns>
    internal bool HandleZoomKey(Key key)
    {
        if (key is Key.OemPlus or Key.Add)
        {
            ZoomIn();
        }
        else if (key is Key.OemMinus or Key.Subtract)
        {
            ZoomOut();
        }
        else if (key is Key.D0 or Key.NumPad0)
        {
            Fit();
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>Puts the view where an unfinished transition was going, now.</summary>
    internal void Settle()
    {
        if (_move is { } move)
        {
            _move = null;
            Apply(move.To);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dots = null;   // the brushes it reads come from the tree it is now in
        Apply(View);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Settle();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Size.Width * change.GetOldValue<Rect>().Size.Height <= 0)
        {
            Keep();   // the first real size: a world set before layout is checked now
        }
    }

    // The view with the world kept on the stage; before the stage has a size there is nothing to keep it on.
    private ZoomView Kept(ZoomView view) => Bounds.Width <= 0 || Bounds.Height <= 0 ? view : view.Kept(Bounds.Size, WorldBounds);

    private void Keep() => Apply(Kept(View));

    // The move to a view: at once under reduced motion or with nothing to draw frames on, else over Motion.Base.
    private void MoveTo(ZoomView target)
    {
        Settle();
        var length = this.TryFindResource("Motion.Base", ActualThemeVariant, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;
        if (length <= TimeSpan.Zero || TopLevel.GetTopLevel(this) is not { } top || target == View)
        {
            Apply(target);
            return;
        }

        var id = ++_moves;
        _move = (View, target, null, length, id);
        top.RequestAnimationFrame(now => Frame(now, id));
    }

    // One chain of frames per move: a callback from a move that was settled or replaced finds another id and stops.
    private void Frame(TimeSpan now, int id)
    {
        if (_move is not { } move || move.Id != id)
        {
            return;
        }

        var start = move.Start ?? now;
        var t = Math.Clamp((now - start) / move.Length, 0, 1);
        if (t >= 1)
        {
            Settle();
            return;
        }

        _move = move with { Start = start };
        Apply(ZoomView.Lerp(move.From, move.To, 1 - Math.Pow(1 - t, 3)));
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(next => Frame(next, id));
    }

    private void Apply(ZoomView view)
    {
        var changed = view != View;
        var old = View;
        View = view;
        var matrix = new Matrix(view.Scale, 0, 0, view.Scale, view.X, view.Y);
        if (Child is { } world)
        {
            world.RenderTransformOrigin = RelativePoint.TopLeft;
            world.RenderTransform = new ImmutableTransform(matrix);
        }

        _dots ??= Dots();
        _dots.Transform = new ImmutableTransform(matrix);
        Background = _dots;
        if (changed)
        {
            RaisePropertyChanged(ViewProperty, old, view);
        }
    }

    // The 22 px dot grid in HairlineStrong (each dot centred on the pixel at 11,11 so it paints that pixel whole, not a quarter of it), in a tile that scales and moves with the view, over a transparent base so the pointer still lands here.
    private DrawingBrush Dots()
    {
        var dot = this.TryFindResource("Brush.HairlineStrong", ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Gray;
        var tile = new Rect(0, 0, DotPitch, DotPitch);
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(tile) },
                new GeometryDrawing { Brush = dot, Geometry = new EllipseGeometry(new Rect((DotPitch / 2) - 0.5, (DotPitch / 2) - 0.5, 2, 2)) },
            },
        };
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(tile, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(tile, RelativeUnit.Absolute),
        };
    }
}
