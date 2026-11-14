using Avalonia;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The action strip of a flat <see cref="Row"/> (#3189): the row's only child control, and only while the row is hot.</summary>
internal sealed partial class Row
{
    /// <summary>The strip the row shows now, or null when the row is idle or has no actions.</summary>
    internal RowActionsStrip? Strip { get; private set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Strip?.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Strip is { } strip)
        {
            var size = strip.DesiredSize;
            strip.Arrange(new Rect(finalSize.Width - MetricsNow().Inset - size.Width, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }

        return finalSize;
    }

    // Hot is the pointer over the row or keyboard focus within it (the cursor). A hot row with actions takes a strip; a cold one, a recycled one or one with no actions gives it back.
    private void UpdateStrip()
    {
        var actions = DataContext is IRowItem { Actions.Count: > 0 } item ? item.Actions : null;
        if (actions is null || !(IsPointerOver || IsKeyboardFocusWithin))
        {
            DropStrip();
            return;
        }

        if (Strip is null)
        {
            Strip = RowActionsStrip.Rent();
            LogicalChildren.Add(Strip);
            VisualChildren.Add(Strip);
        }

        Strip.Fill(actions);
        InvalidateMeasure();
    }

    private void DropStrip()
    {
        if (Strip is null)
        {
            return;
        }

        _ = VisualChildren.Remove(Strip);
        _ = LogicalChildren.Remove(Strip);
        RowActionsStrip.Return(Strip);
        Strip = null;
        InvalidateMeasure();
    }
}
