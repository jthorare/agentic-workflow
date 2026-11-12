using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The sticky group header of <see cref="RowList"/> (#2978): the overlay that follows the scroll.</summary>
internal sealed partial class RowList
{
    /// <summary>The group whose header the sticky header shows now; null while the list is at its top.</summary>
    internal object? StickyGroup => _sticky is { IsVisible: true } sticky ? sticky.Group : null;

    /// <summary>How far the sticky header is pushed up by the next group's header.</summary>
    internal double StickyPush => _sticky?.RenderTransform is TranslateTransform t ? t.Y : 0;

    // The header that sticks is the last one whose group has reached the top; the next group's header pushes it out as it arrives.
    private void UpdateSticky()
    {
        if (_sticky is null || _scroll is null)
        {
            return;
        }

        var offset = _scroll.Offset.Y;
        if (_groups.Count == 0 || offset <= 0.5)
        {
            _sticky.IsVisible = false;
            return;
        }

        var active = 0;
        for (var i = 0; i < _groupTops.Count; i++)
        {
            if (_groupTops[i] <= offset + 0.5)
            {
                active = i;
            }
        }

        var next = active + 1 < _groupTops.Count ? _groupTops[active + 1] : double.PositiveInfinity;
        if (!ReferenceEquals(_sticky.Group, _groups[active]))
        {
            _sticky.Fill(_groups[active]);
        }

        _sticky.RenderTransform = new TranslateTransform(0, StickyHeader.PushOut(next, offset, HeightOf("Row.GroupHeader", 28)));
        _sticky.IsVisible = true;
    }

    /// <inheritdoc />
    public void RevealRow(int index)
    {
        ScrollIntoView(index);
        UpdateLayout();
        if (_scroll is not { } scroll || ContainerFromIndex(index) is not { } container || scroll.Offset.Y <= 0)
        {
            return;
        }

        // ScrollIntoView puts a row above the view flush with the top edge, which is under the opaque sticky header: move it down by the header's height.
        var inset = HeightOf("Row.GroupHeader", 28);
        if (container.TranslatePoint(default, scroll)?.Y is { } top && top < inset)
        {
            scroll.Offset = new Vector(0, Math.Max(0, scroll.Offset.Y - (inset - top)));
            UpdateLayout();
        }
    }

    private double HeightOf(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;
}
