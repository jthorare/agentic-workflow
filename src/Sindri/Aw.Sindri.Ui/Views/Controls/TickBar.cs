using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // placed by XAML

/// <summary>
/// A backlog as ticks: one tick per record up to Size.Tick.Max, then each tick stands for ceil(Total / Max) records. The first
/// round(Value / Total * ticks) ticks are lit (Brush.Chart.Tick), the rest dim (Brush.Chart.TickDim). Total 0 draws nothing.
/// </summary>
internal sealed class TickBar : Control
{
    /// <summary>The count that is done (lit).</summary>
    internal static readonly StyledProperty<int> ValueProperty = AvaloniaProperty.Register<TickBar, int>(nameof(Value));

    /// <summary>The count of everything.</summary>
    internal static readonly StyledProperty<int> TotalProperty = AvaloniaProperty.Register<TickBar, int>(nameof(Total));

    private const int DefaultMax = 40;
    private const double DefaultW = 5;
    private const double DefaultH = 20;
    private const double DefaultGap = 4;
    private const double TickRadius = 1;

    static TickBar()
    {
        AffectsRender<TickBar>(ValueProperty, TotalProperty);
        AffectsMeasure<TickBar>(TotalProperty);
    }

    /// <summary>Gets or sets the lit count.</summary>
    internal int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets the total.</summary>
    internal int Total
    {
        get => GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    /// <summary>How many ticks are drawn: Total up to Size.Tick.Max, none for a Total of 0.</summary>
    internal int Ticks => Total <= 0 ? 0 : Math.Min(Total, Max);

    /// <summary>How many of the ticks are lit.</summary>
    internal int Lit => Total <= 0 ? 0 : Math.Clamp((int)Math.Round((double)Value / Total * Ticks, MidpointRounding.AwayFromZero), 0, Ticks);

    private int Max => Math.Max(1, (int)Number("Size.Tick.Max", DefaultMax));

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var ticks = Ticks;
        return ticks == 0 ? default : new Size((ticks * Number("Size.Tick.W", DefaultW)) + ((ticks - 1) * Number("Size.Tick.Gap", DefaultGap)), Number("Size.Tick.H", DefaultH));
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var (ticks, lit) = (Ticks, Lit);
        var (w, h, gap) = (Number("Size.Tick.W", DefaultW), Number("Size.Tick.H", DefaultH), Number("Size.Tick.Gap", DefaultGap));
        var on = Brush("Brush.Chart.Tick");
        var off = Brush("Brush.Chart.TickDim");
        for (var i = 0; i < ticks; i++)
        {
            context.DrawRectangle(i < lit ? on : off, pen: null, new RoundedRect(new Rect(i * (w + gap), 0, w, h), radius: TickRadius));
        }
    }

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush found ? found : Brushes.Gray;

    private double Number(string key, double fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IConvertible c ? c.ToDouble(CultureInfo.InvariantCulture) : fallback;
}

#pragma warning restore MA0182
