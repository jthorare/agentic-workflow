using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The test hooks of the flat Row (#3189): what it drew now, and the render-scaling change the headless platform can't raise itself.</summary>
internal sealed partial class Row
{
    /// <summary>What the row drew now: its texts, ink, cells, chips, selection bar and ring, read after the same layout pass Render uses.</summary>
    internal RowLayout Layout
    {
        get
        {
            var item = DataContext as IRowItem ?? throw new InvalidOperationException("The row has no item to lay out.");
            var size = Bounds.Size;
            Prepare(item, size);
            var m = MetricsNow();
            var title = Built(Title);
            var titleX = _slots.TitleX + Built(Id).Width + TitleGap;
            return new RowLayout(
                item.Id,
                item.Title,
                item.SubLine ?? string.Empty,
                item.Age ?? string.Empty,
                WaitText(item),
                [.. _cellText.Take(_shown).Select(t => t ?? string.Empty)],
                ChipsOf(item),
                _titleInk,
                InksNow().Subtle,
                FontsNow().Caption,
                _layoutsBuilt,
                InksNow().Subtle,
                CellsOf(item, size.Height),
                new Rect(_slots.AgeX, 0, m.AgeWidth, size.Height),
                new Rect(titleX, 0, title.Width, size.Height),
                _titleTrimmed,
                _titleDesired,
                Classes.Contains("sel") ? new Rect(0, 0, SelBarWidth, size.Height) : null,
                Brush("Brush.Primary"),
                Classes.Contains("cursor"),
                _text[Sub] is { } sub ? new Rect(titleX + title.Width + TitleGap, 0, sub.Width, size.Height) : null,
                new Rect(_slots.WaitX, 0, m.WaitWidth, size.Height),
                new Rect(_slots.TitleX, 0, _slots.TitleWidth, size.Height));
        }
    }

    /// <summary>The top level the row listens to for render-scaling changes: set while attached, null once detached.</summary>
    internal TopLevel? ScalingSource { get; private set; }

    /// <summary>Runs what a render-scaling change runs, since the headless platform can't change the scaling itself.</summary>
    internal void ScalingChangedForTest() => OnScalingChanged(this, EventArgs.Empty);

    private List<RowCell> CellsOf(IRowItem item, double height)
    {
        var cells = new List<RowCell>();
        var m = MetricsNow();
        var x = _slots.FactsX;
        var columns = Columns ?? [];
        for (var i = 0; i < _shown; i++)
        {
            x += Gap;
            var text = _facts[2 * i];
            if (item.Facts.ContainsKey(columns[i].Key) && text is not null)
            {
                cells.Add(new RowCell(columns[i].Key, new Rect(x, 0, m.Columns[i], height), _cellText[i] ?? string.Empty, text.Width));
            }

            x += m.Columns[i];
        }

        return cells;
    }

    private List<RowChip> ChipsOf(IRowItem item)
    {
        var chips = new List<RowChip>();
        var columns = Columns ?? [];
        for (var i = 0; i < _shown; i++)
        {
            if (item.Facts.TryGetValue(columns[i].Key, out var fact) && fact.Kind == RowFactKind.Chip)
            {
                var brushes = Chip(fact.Tone);
                chips.Add(new RowChip(fact.Text, _toneKeys[(int)fact.Tone].ToLowerInvariant(), brushes.Fill, brushes.Ink));
            }
        }

        return chips;
    }
}
