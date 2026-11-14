using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>Draws one frame of a <see cref="TerminalGrid"/> onto a drawing context. The theme brushes arrive resolved and the colour brushes come from a cache that outlives the paint, so a paint makes no brush.</summary>
/// <param name="context">Where to draw.</param>
/// <param name="cell">The size of one cell.</param>
/// <param name="face">The mono face.</param>
/// <param name="fontSize">The font size in px.</param>
/// <param name="ink">The theme's default text colour.</param>
/// <param name="canvas">The theme's page colour (the background of an inverse cell on the default background).</param>
/// <param name="brushes">The colour brushes, kept between paints.</param>
internal sealed class TerminalPainter(DrawingContext context, Size cell, Typeface face, double fontSize, IBrush ink, IBrush canvas, TerminalBrushCache brushes)
{
    private const double DimOpacity = 0.55;
    private readonly Typeface[] _faces = new Typeface[4];

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

        var run = new StringBuilder(grid.Width);
        for (var y = 0; y < grid.Height; y++)
        {
            PaintText(grid, top + y, y, run);
        }

        if (back == 0 && !grid.Dead)
        {
            PaintCursor(grid, top, focused);
        }
    }

    // The background brush of a cell, null for the default background.
    private IBrush? Background(TerminalCell c) => c.Inverse ? brushes.Get(c.Fg) ?? ink : brushes.Get(c.Bg);

    private IBrush Foreground(TerminalCell c) => c.Inverse ? brushes.Get(c.Bg) ?? canvas : brushes.Get(c.Fg) ?? ink;

    private void PaintBackgrounds(TerminalGrid grid, int row, int y)
    {
        var start = 0;
        while (start < grid.Width)
        {
            var bg = Background(grid.Cell(row, start));
            var end = start + 1;
            while (end < grid.Width && ReferenceEquals(Background(grid.Cell(row, end)), bg))
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

    private void PaintText(TerminalGrid grid, int row, int y, StringBuilder run)
    {
        var start = 0;
        while (start < grid.Width)
        {
            var first = grid.Cell(row, start);
            var end = RunEnd(grid, row, start);
            _ = run.Clear();
            for (var x = start; x < end; x++)
            {
                _ = run.Append(grid.Cell(row, x).Text);
            }

            if (HasInk(run))
            {
                DrawRun(run.ToString(), first, new Point(start * cell.Width, y * cell.Height));
            }

            start = end;
        }
    }

    private static bool HasInk(StringBuilder run)
    {
        for (var i = 0; i < run.Length; i++)
        {
            if (!char.IsWhiteSpace(run[i]))
            {
                return true;
            }
        }

        return false;
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

    private Typeface FaceOf(TerminalCell style)
    {
        var index = (style.Italic ? 2 : 0) + (style.Bold ? 1 : 0);
        if (_faces[index].FontFamily is null)
        {
            _faces[index] = new Typeface(face.FontFamily, style.Italic ? FontStyle.Italic : FontStyle.Normal, style.Bold ? FontWeight.Bold : FontWeight.Normal);
        }

        return _faces[index];
    }

    private void DrawRun(string text, TerminalCell style, Point at)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, FaceOf(style), fontSize, Foreground(style));
        if (style.Underline)
        {
            formatted.SetTextDecorations(TextDecorations.Underline);
        }

        if (style.Dim)
        {
            using (context.PushOpacity(DimOpacity))
            {
                context.DrawText(formatted, at);
            }

            return;
        }

        context.DrawText(formatted, at);
    }

    // The cursor goes over the text: a filled block in Ink with the glyph under it redrawn in Canvas (ink on ink would hide it), or a hollow box that leaves the glyph as drawn.
    private void PaintCursor(TerminalGrid grid, int top, bool focused)
    {
        if (grid.CursorX >= grid.Width || grid.CursorY >= grid.Height)
        {
            return;
        }

        var at = new Point(grid.CursorX * cell.Width, grid.CursorY * cell.Height);
        var rect = new Rect(at, cell);
        if (!focused)
        {
            context.DrawRectangle(brush: null, pen: new Pen(ink, 1), rect: rect.Deflate(0.5));
            return;
        }

        context.DrawRectangle(brush: ink, pen: null, rect: rect);
        var under = grid.Cell(top + grid.CursorY, grid.CursorX);
        if (!string.IsNullOrWhiteSpace(under.Text))
        {
            context.DrawText(new FormattedText(under.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, FaceOf(under), fontSize, canvas), at);
        }
    }
}
