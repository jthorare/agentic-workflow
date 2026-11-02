using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Icons;

/// <summary>
/// A state glyph (UI spec §11.3) or one of the shell's marks, drawn as a vector icon from Theme/Icons.axaml (R1): the
/// geometry sits on a 16 × 16 grid and is scaled to the control's size (Size.Icon, the Body line, scaled with the text)
/// and stroked at 1.5 px with round caps; the brand mark is filled. It never draws a font glyph, so it renders the same
/// on every OS. A state icon is decorative: the word beside it carries the state, so it is hidden from assistive tech.
/// The :ok, :down, :warn and :stale pseudo-classes follow <see cref="Glyph"/> for the icon's colour styles. With
/// <see cref="Spins"/> set (the in-flight spinner, §4.1) it turns once per Motion.Spin, and stands still when reduced
/// motion has set that duration to 0 (§11.4); the turn carries no information of its own.
/// </summary>
internal sealed class StateIcon : Control
{
    /// <summary>The state glyph to draw; ignored when <see cref="Icon"/> names a mark.</summary>
    public static readonly StyledProperty<StateGlyph> GlyphProperty = AvaloniaProperty.Register<StateIcon, StateGlyph>(nameof(Glyph));

    /// <summary>A mark to draw by its Icons.axaml name ("Brand", "Refresh", "Close", "Command", "Spinner").</summary>
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<StateIcon, string?>(nameof(Icon));

    /// <summary>The brush the icon is drawn with (its styles set a Brush.* token).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<StateIcon, IBrush?>(nameof(Foreground));

    /// <summary>The geometry drawn, resolved from Icons.axaml.</summary>
    public static readonly StyledProperty<Geometry?> GeometryProperty = AvaloniaProperty.Register<StateIcon, Geometry?>(nameof(Geometry));

    /// <summary>True to turn the icon (the spinner).</summary>
    public static readonly StyledProperty<bool> SpinsProperty = AvaloniaProperty.Register<StateIcon, bool>(nameof(Spins));

    /// <summary>The icon's turn in degrees, animated while it spins.</summary>
    public static readonly StyledProperty<double> AngleProperty = AvaloniaProperty.Register<StateIcon, double>(nameof(Angle));

    // Stops the turn in flight, if any.
    private Action? _spin;

    // The Motion.Spin the turn in flight runs on.
    private TimeSpan _spinning;

    // The grid every icon is drawn on, and its stroke.
    private const double Grid = 16;
    private const double Stroke = 1.5;

    static StateIcon()
    {
        AffectsRender<StateIcon>(ForegroundProperty, GeometryProperty, AngleProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<StateIcon>(AccessibilityView.Raw);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateIcon"/> class. A change to the app's resources re-evaluates
    /// the turn, so reduced motion switched on mid-turn (Motion.Spin set to 0) stops it where it is, and switched off
    /// starts it again (§11.4). The handler is the icon's own, so it goes with the icon.
    /// </summary>
    public StateIcon()
    {
        ResourcesChanged += (_, _) => Turn();
    }

    /// <summary>Gets or sets the state glyph.</summary>
    public StateGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Gets or sets the mark's name.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets the icon's brush.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the icon should turn.</summary>
    public bool Spins
    {
        get => GetValue(SpinsProperty);
        set => SetValue(SpinsProperty, value);
    }

    /// <summary>Gets or sets the icon's turn in degrees.</summary>
    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    /// <summary>Gets a value indicating whether the icon is turning (the spinner, unless motion is reduced).</summary>
    public bool IsSpinning => _spin is not null;

    /// <summary>Gets the geometry drawn, or null when there is nothing to draw (no glyph).</summary>
    public Geometry? Geometry => GetValue(GeometryProperty);

    // The resource key for what this icon draws, or null for none.
    private string? Key => Icon is { Length: > 0 } icon ? "Icon." + icon : Glyph == StateGlyph.None ? null : "Icon." + Glyph;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Geometry is not { } geometry || Foreground is not { } brush)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width, Bounds.Height) / Grid;
        var offset = new Vector((Bounds.Width - (Grid * scale)) / 2, (Bounds.Height - (Grid * scale)) / 2);
        var turn = Matrix.CreateTranslation(-Grid / 2, -Grid / 2) * Matrix.CreateRotation(Matrix.ToRadians(Angle)) * Matrix.CreateTranslation(Grid / 2, Grid / 2);
        using (context.PushTransform(turn * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset)))
        {
            if (string.Equals(Icon, "Brand", StringComparison.Ordinal))
            {
                context.DrawGeometry(brush, pen: null, geometry);
            }
            else
            {
                context.DrawGeometry(brush: null, new Pen(brush, Stroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Resolve();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty || change.Property == IconProperty)
        {
            PseudoClasses.Set(":ok", Glyph == StateGlyph.Ok);
            PseudoClasses.Set(":down", Glyph == StateGlyph.Down);
            PseudoClasses.Set(":stale", Glyph == StateGlyph.Stale);
            PseudoClasses.Set(":warn", Glyph == StateGlyph.Warning);
            Resolve();
        }
        else if (change.Property == SpinsProperty)
        {
            Turn();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Turn();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    // Starts or stops the turn: it runs while Spins is set, the icon is in a window and Motion.Spin is above 0. A turn
    // already running on another Motion.Spin starts over on the new one.
    private void Turn()
    {
        var duration = this.TryFindResource("Motion.Spin", out var value) && value is TimeSpan spin ? spin : TimeSpan.Zero;
        if (!Spins || TopLevel.GetTopLevel(this) is null || duration <= TimeSpan.Zero)
        {
            Stop();
            return;
        }

        if (_spin is not null)
        {
            if (duration == _spinning)
            {
                return;
            }

            Stop();
        }

        _spinning = duration;

#pragma warning disable CA2000 // disposed by the _spin stop action below
        var turning = new CancellationTokenSource();
#pragma warning restore CA2000
        _spin = () =>
        {
            turning.Cancel();
            turning.Dispose();
        };
        var animation = new Animation
        {
            Duration = duration,
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(AngleProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(AngleProperty, 360d) } },
            },
        };
        _ = animation.RunAsync(this, turning.Token);
    }

    private void Stop()
    {
        if (_spin is { } spin)
        {
            _spin = null;
            spin();
        }

        Angle = 0;
    }

    private void Resolve() =>
        SetValue(GeometryProperty, Key is { } key && this.TryFindResource(key, out var value) ? value as Geometry : null);
}
