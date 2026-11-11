using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The sticky group list (<see cref="StickyGroupList"/>, continued): which header sticks, read from the rows built in view, and how the flat list draws a header or a row.</summary>
internal sealed partial class StickyGroupList
{
    // The built rows and headers, in list order, with their positions in the flat list.
    private List<(int Index, Control Container)> Realized()
    {
        var realized = new List<(int, Control)>();
        if (_items.ItemsPanelRoot is { } panel)
        {
            foreach (var child in panel.Children.OfType<Control>().Where(c => c.IsVisible))
            {
                var index = _items.IndexFromContainer(child);
                if (index >= 0 && index < _groupOf.Count)
                {
                    realized.Add((index, child));
                }
            }
        }

        realized.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return realized;
    }

    // A header's own height: a built header's, else the sticky one's.
    private double HeaderHeight(List<(int Index, Control Container)> realized) =>
        realized.FirstOrDefault(r => _flat[r.Index] is IRowGroup).Container?.Bounds.Height is { } built and > 0 ? built : _sticky.Bounds.Height;

    // The header that sticks is the one of the first row in view; the next group's header pushes it out as it arrives (when it is built it is near).
    private void FollowScroll()
    {
        var offset = _scroll.Offset.Y;
        if (_order.Count == 0 || offset <= 0.5)
        {
            ShowSticky(group: null, push: 0);
            return;
        }

        var realized = Realized();
        var first = realized.FirstOrDefault(r => r.Container.TranslatePoint(default, _scroll) is { } at && at.Y + r.Container.Bounds.Height > 0.5);
        if (first.Container is null)
        {
            return;
        }

        var active = _groupOf[first.Index];
        var position = _order.FindIndex(g => ReferenceEquals(g, active));
        var height = HeaderHeight(realized);
        var push = 0.0;
        if (position + 1 < _order.Count)
        {
            var nextIndex = _headerAt[_order[position + 1]];
            var next = realized.FirstOrDefault(r => r.Index == nextIndex);
            if (next.Container?.TranslatePoint(default, _scroll) is { } top)
            {
                push = Math.Min(0, top.Y - height);
            }
        }

        ShowSticky(active, push);
    }

    private void ShowSticky(object? group, double push)
    {
        if (!ReferenceEquals(_sticky.Content, group))
        {
            _sticky.Content = group;
        }

        _sticky.IsVisible = group is not null;
        if (Math.Abs(_push.Y - push) > 0.01)
        {
            _push.Y = push;
        }
    }

    // Draws a group as its header and any other entry as a row: the data context of what is built is the group or the row itself.
    private sealed class FlatTemplate(StickyGroupList owner) : IDataTemplate
    {
        public bool Match(object? data) => data is not null;

        public Control? Build(object? param) =>
            param is IRowGroup ? owner.HeaderTemplate?.Build(param) : owner.ItemTemplate?.Build(param);
    }
}
