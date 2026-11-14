using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The fact cells of a flat <see cref="Row"/> (#3189): chip, progress bar, diffstat, ref, person, count and text. Every text ends with an ellipsis in its cell.</summary>
internal sealed partial class Row
{
    private const double BarWidth = 56;
    private const double BarHeight = 4;
    private const double Avatar = 16;
    private const double ChipPad = 14;   // 6 px each side and the 1 px border

    private void BuildFacts(IRowItem item, Metrics m)
    {
        if (_facts.Length != 2 * m.Columns.Length)
        {
            _facts = new TextLayout?[2 * m.Columns.Length];
            _cellText = new string?[m.Columns.Length];
        }

        var columns = Columns ?? [];
        for (var i = 0; i < _shown; i++)
        {
            _cellText[i] = null;
            if (item.Facts.TryGetValue(columns[i].Key, out var fact))
            {
                BuildFact(fact, i, m.Columns[i]);
            }
        }
    }

    private void BuildFact(RowFact fact, int index, double width)
    {
        var f = FontsNow();
        var k = InksNow();
        var text = fact.Kind == RowFactKind.Ref ? RefFit.Fit(this, fact.Text, width, MonoChar) : fact.Text;
        _cellText[index] = text;
        switch (fact.Kind)
        {
            case RowFactKind.Chip:
                _facts[2 * index] ??= Make(text, f.Chip, f.Caption, Chip(fact.Tone).Ink, new Fit(width - ChipPad));
                break;
            case RowFactKind.Progress:
                _facts[2 * index] ??= Make(text, f.Text, f.Caption, k.Subtle, new Fit(width - BarWidth - 8));
                break;
            case RowFactKind.Person:
                _facts[2 * index] ??= Make(text, f.Text, f.Caption, k.Subtle, new Fit(width - Avatar - TitleGap));
                _facts[(2 * index) + 1] ??= Make(text.Length > 0 ? text[..1].ToUpperInvariant() : string.Empty, f.Text, 9, k.Ink);
                break;
            case RowFactKind.Diffstat or RowFactKind.Ref:
                _facts[2 * index] ??= Make(text, f.Mono, f.MonoSize, k.Subtle, new Fit(width));
                break;
            case RowFactKind.Text or RowFactKind.Count:
            default:
                _facts[2 * index] ??= Make(text, f.Text, f.Caption, k.Subtle, new Fit(width));
                break;
        }
    }

    private void DrawFacts(DrawingContext context, IRowItem item, double height)
    {
        var m = MetricsNow();
        var x = _slots.FactsX;
        var columns = Columns ?? [];
        for (var i = 0; i < _shown; i++)
        {
            x += Gap;
            if (item.Facts.TryGetValue(columns[i].Key, out var fact))
            {
                DrawFact(context, fact, i, new Rect(x, 0, m.Columns[i], height));   // the texts end with an ellipsis inside their cell, so no clip
            }

            x += m.Columns[i];
        }
    }

    private void DrawFact(DrawingContext context, RowFact fact, int index, Rect cell)
    {
        switch (fact.Kind)
        {
            case RowFactKind.Chip:
                DrawChip(context, fact, index, cell);
                break;
            case RowFactKind.Progress:
                DrawProgress(context, fact, index, cell);
                break;
            case RowFactKind.Person:
                DrawPerson(context, index, cell);
                break;
            case RowFactKind.Text or RowFactKind.Diffstat or RowFactKind.Ref or RowFactKind.Count:
            default:
                Put(context, _facts[2 * index], cell.X, cell.Height);
                break;
        }
    }

    private void DrawChip(DrawingContext context, RowFact fact, int index, Rect cell)
    {
        var text = BuiltFact(2 * index);
        var brushes = Chip(fact.Tone);
        var m = MetricsNow();
        var height = Math.Max(20, FontsNow().LineHeight + 2);   // MinHeight 20, and the caption line with its border at a larger text scale
        var box = new Rect(cell.X, (cell.Height - height) / 2, Math.Min(cell.Width, text.Width + ChipPad), height);
        context.DrawRectangle(brushes.Fill, new Pen(brushes.Border, 1), box.Deflate(0.5), m.ChipRadius, m.ChipRadius);
        text.Draw(context, new Point(box.X + (ChipPad / 2), (cell.Height - text.Height) / 2));
    }

    private void DrawProgress(DrawingContext context, RowFact fact, int index, Rect cell)
    {
        var k = InksNow();
        var bar = new Rect(cell.X, (cell.Height - BarHeight) / 2, BarWidth, BarHeight);
        context.DrawRectangle(k.Surface3, pen: null, bar, 2, 2);
        var share = fact.Total > 0 ? Math.Clamp((double)fact.Done / fact.Total, 0, 1) : 0;
        if (share > 0)
        {
            context.DrawRectangle(k.Primary, pen: null, new Rect(bar.X, bar.Y, BarWidth * share, BarHeight), 2, 2);
        }

        Put(context, _facts[2 * index], cell.X + BarWidth + 8, cell.Height);
    }

    private void DrawPerson(DrawingContext context, int index, Rect cell)
    {
        context.DrawEllipse(InksNow().Surface3, pen: null, new Point(cell.X + (Avatar / 2), cell.Height / 2), Avatar / 2, Avatar / 2);
        var initial = BuiltFact((2 * index) + 1);
        initial.Draw(context, new Point(cell.X + ((Avatar - initial.Width) / 2), (cell.Height - initial.Height) / 2));
        Put(context, _facts[2 * index], cell.X + Avatar + TitleGap, cell.Height);
    }

    // A layout centred on the row's height at x.
    private static void Put(DrawingContext context, TextLayout? layout, double x, double height) =>
        layout?.Draw(context, new Point(x, (height - layout.Height) / 2));
}
