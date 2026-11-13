using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The 14 px state glyph of a row, drawn in a 16 px box (#2978 grammar). Colour means status only. The flat Row draws glyphs itself; this control stays until the tests that find it move (#3189 slice 4).</summary>
[SuppressMessage("Design", "MA0182", Justification = "Used by the Frames tests until the flat Row slices move them (#3189 slice 4).")]
internal sealed class RowGlyphView : Control
{
    /// <summary>The glyph to draw.</summary>
    public static readonly StyledProperty<RowGlyph> GlyphProperty = AvaloniaProperty.Register<RowGlyphView, RowGlyph>(nameof(Glyph));

    private RowGlyphPainter.Palette? _palette;
    private Avalonia.Styling.ThemeVariant? _variant;

    static RowGlyphView()
    {
        AffectsRender<RowGlyphView>(GlyphProperty);
    }

    public RowGlyphView()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is null || Equals(_variant, ActualThemeVariant))
            {
                return;   // detached, or a recycled glyph re-entering the tree under the theme its palette was built for
            }

            _variant = ActualThemeVariant;
            _palette = null;
            InvalidateVisual();
        };
    }

    /// <summary>How many geometries, pens and palettes the glyphs ever built.</summary>
    internal static long Allocations => RowGlyphPainter.Allocations;

    /// <summary>The glyph to draw.</summary>
    public RowGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(width: 16, height: 16);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _palette ??= RowGlyphPainter.PaletteFor(this);
        RowGlyphPainter.Paint(context, Glyph, _palette);
    }
}
