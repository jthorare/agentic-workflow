using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>Draws one frame of a <see cref="TerminalGrid"/> onto a drawing context.</summary>
/// <param name="context">Where to draw.</param>
/// <param name="cell">The size of one cell.</param>
/// <param name="face">The mono face.</param>
/// <param name="fontSize">The font size in px.</param>
/// <param name="theme">Looks a theme brush up by key (the default text and background come from it).</param>
internal sealed class TerminalPainter(DrawingContext context, Size cell, Typeface face, double fontSize, Func<string, IBrush> theme)
{
    /// <summary>Draws the visible rows and the cursor.</summary>
    /// <param name="grid">The frame.</param>
    /// <param name="scrollOffset">Rows back from the live screen; 0 is live.</param>
    /// <param name="focused">True for a filled cursor, false for a hollow one.</param>
    internal void Paint(TerminalGrid grid, int scrollOffset, bool focused)
    {
        var back = Math.Clamp(scrollOffset, 0, grid.History);
        var top = grid.Rows - grid.Height - back;
        for (var y = 0; y < grid.Height; y++)
        {
            PaintBackgrounds(grid, top + y, y);
        }

        if (back == 0 && !grid.Dead)
        {
            PaintCursor(grid, focused);
        }

        for (var y = 0; y < grid.Height; y++)
        {
            PaintText(grid, top + y, y);
        }
    }

    private (IBrush? Bg, IBrush Fg) Colours(TerminalCell c)
    {
        var fg = Solid(c.Fg);
        var bg = Solid(c.Bg);
        return c.Inverse
            ? (fg ?? theme("Brush.Ink"), bg ?? theme("Brush.Canvas"))
            : (bg, fg ?? theme("Brush.Ink"));
    }

    private static ImmutableSolidColorBrush? Solid(TerminalColor color) =>
        TerminalPalette.Resolve(color) is { } c ? new ImmutableSolidColorBrush(c) : null;

    private void PaintBackgrounds(TerminalGrid grid, int row, int y)
    {
        var start = 0;
        while (start < grid.Width)
        {
            var bg = Colours(grid.Cell(row, start)).Bg;
            var end = start + 1;
            while (end < grid.Width && SameBrush(Colours(grid.Cell(row, end)).Bg, bg))
            {
                end++;
            }

            if (bg is not null)
            {
                context.DrawRectangle(brush: bg, pen: null, rect: new Rect(start * cell.Width, y * cell.Height, (end - start) * cell.Width, cell.Height));
            }

            start = end;
        }
    }

    private static bool SameBrush(IBrush? a, IBrush? b) =>
        a is ImmutableSolidColorBrush x && b is ImmutableSolidColorBrush y ? x.Color == y.Color : ReferenceEquals(a, b);

    private void PaintText(TerminalGrid grid, int row, int y)
    {
        var start = 0;
        while (start < grid.Width)
        {
            var first = grid.Cell(row, start);
            var end = RunEnd(grid, row, start);
            var text = string.Concat(Enumerable.Range(start, end - start).Select(x => grid.Cell(row, x).Text));
            if (text.Trim().Length > 0)
            {
                DrawRun(text, first, new Point(start * cell.Width, y * cell.Height));
            }

            start = end;
        }
    }

    // A run ends where colour or attributes change, and after a wide character (its glyph may not match the cell width).
    private static int RunEnd(TerminalGrid grid, int row, int start)
    {
        var first = grid.Cell(row, start);
        if (first.Columns == 2)
        {
            return start + 2;
        }

        var end = start + 1;
        while (end < grid.Width)
        {
            var next = grid.Cell(row, end);
            if (next.Columns != 1 || next.Fg != first.Fg || next.Bg != first.Bg || next.Style != first.Style)
            {
                break;
            }

            end++;
        }

        return end;
    }

    private void DrawRun(string text, TerminalCell style, Point at)
    {
        var typeface = new Typeface(face.FontFamily, style.Italic ? FontStyle.Italic : FontStyle.Normal, style.Bold ? FontWeight.Bold : FontWeight.Normal);
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Colours(style).Fg);
        if (style.Underline)
        {
            formatted.SetTextDecorations(TextDecorations.Underline);
        }

        context.DrawText(formatted, at);
    }

    private void PaintCursor(TerminalGrid grid, bool focused)
    {
        if (grid.CursorX >= grid.Width || grid.CursorY >= grid.Height)
        {
            return;
        }

        var rect = new Rect(grid.CursorX * cell.Width, grid.CursorY * cell.Height, cell.Width, cell.Height);
        var ink = theme("Brush.Ink");
        if (focused)
        {
            using (context.PushOpacity(0.55))
            {
                context.DrawRectangle(brush: ink, pen: null, rect: rect);
            }
        }
        else
        {
            context.DrawRectangle(brush: null, pen: new Pen(ink, 1), rect: rect.Deflate(0.5));
        }
    }
}
