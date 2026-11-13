using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
        var states = _run.States;
        if (states.Count == 0 || offset <= 0.5)
        {
            _sticky.IsVisible = false;
            return;
        }

        var headerHeight = HeightOf("Row.GroupHeader", 28);
        var (active, next) = ActiveGroup(offset, headerHeight);
        if (!ReferenceEquals(_sticky.Group, states[active].Group))
        {
            _sticky.Fill(states[active].Group);
        }

        _sticky.RenderTransform = new TranslateTransform(0, StickyHeader.PushOut(next, offset, headerHeight));
        _sticky.IsVisible = true;
        SyncStickyFocus();
    }

    // The sticky copy never takes focus: it mirrors the real header of its group while that one holds the keyboard focus, so the cursor stays visible under the copy.
    private void SyncStickyFocus()
    {
        if (_sticky is null)
        {
            return;
        }

        // Walk up from the focused element, not down through every realized row: a scroll tick costs the tree's depth.
        var focusedHeader = (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual)?
            .GetSelfAndVisualAncestors().OfType<RowGroupHeader>().FirstOrDefault();
        _sticky.MirrorsFocus = _sticky.IsVisible && _sticky.Group is { } group
            && focusedHeader is not null && !ReferenceEquals(focusedHeader, _sticky)
            && ReferenceEquals(focusedHeader.Group, group) && this.IsVisualAncestorOf(focusedHeader);
    }

    private void PostStickyFocusSync() => Dispatcher.UIThread.Post(SyncStickyFocus);

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

    // The list pads its top and bottom, and every group after the first starts GroupGap below the one before (grammar .list, .group).
    internal const double ListPadding = 8;
    internal const double GroupGap = 8;

    // Tops come from counts, not stored pixels: a header is one height and a row another, and the row height follows the density now (an ancestor's class comfortable).
    private (int Active, double NextTop) ActiveGroup(double offset, double headerHeight)
    {
        var rowHeight = RowHeight;
        var states = _run.States;
        var active = 0;
        var top = ListPadding;
        for (var i = 0; i < states.Count; i++)
        {
            if (top > offset + 0.5)
            {
                return (active, top);
            }

            active = i;
            top += headerHeight + (states[i].Visible * rowHeight) + GroupGap;
        }

        return (active, double.PositiveInfinity);
    }

    private double HeightOf(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;
}
