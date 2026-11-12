using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>A group header of a <see cref="RowList"/> (#2978): sentence-case heading, count, optional right-hand summary. 28 px, class group-header.</summary>
internal sealed class RowGroupHeader : Border
{
    private readonly TextBlock _heading = Caption(FontWeight.Medium);
    private readonly TextBlock _count = Caption(FontWeight.Normal);
    private readonly TextBlock _summary = Caption(FontWeight.Normal);

    public RowGroupHeader()
    {
        Classes.Add("group-header");
        _count.Margin = new Thickness(6, 0, 0, 0);
        _summary.HorizontalAlignment = HorizontalAlignment.Right;
        _summary.TextAlignment = TextAlignment.Right;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Children = { _heading, _count } };
        Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left } };
        Grid.SetColumn(_summary, 1);
        ((Grid)Child).Children.Add(_summary);
    }

    /// <summary>The group this header shows; null while the header is recycled.</summary>
    internal IRowGroup? Group { get; private set; }

    /// <summary>Shows a group.</summary>
    /// <param name="group">The group.</param>
    internal void Fill(IRowGroup? group)
    {
        Group = group;
        var heading = group as IRowGroupHeading;
        _heading.Text = heading?.Heading ?? group?.ToString() ?? string.Empty;
        _summary.Text = heading?.Summary;
        _count.Text = group is null ? string.Empty : Count(group.Items).ToString(CultureInfo.InvariantCulture);
    }

    private static int Count(IEnumerable items) => items is ICollection collection ? collection.Count : items.Cast<object>().Count();

    private static TextBlock Caption(FontWeight weight)
    {
        var block = new TextBlock { FontWeight = weight, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        _ = block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("Brush.InkSubtle"));
        _ = block.Bind(TextBlock.FontSizeProperty, block.GetResourceObservable("Type.Caption.Size"));
        return block;
    }
}
