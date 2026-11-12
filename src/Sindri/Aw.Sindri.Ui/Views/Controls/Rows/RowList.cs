using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// A virtualized list of grouped rows with sticky group headers and one shared set of fact columns (#2978). ItemsSource holds IRowGroups of
/// <see cref="IRowItem"/>. It has no key handler of its own: it listens for <see cref="RowKeys"/>' routed events and raises <see cref="PeekRequested"/>,
/// <see cref="OpenRequested"/> and <see cref="BackRequested"/>. Stub: it draws nothing yet.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
[SuppressMessage("Design", "MA0041", Justification = "Stub members throw until the control commit (#2978).")]
internal sealed class RowList : ItemsControl
{
    /// <summary>The fact columns every row shows, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<RowColumn>?> ColumnsProperty = AvaloniaProperty.Register<RowList, IReadOnlyList<RowColumn>?>(nameof(Columns));

    /// <summary>How many fact columns stay while the peek is open.</summary>
    public static readonly StyledProperty<int> PeekColumnsProperty = AvaloniaProperty.Register<RowList, int>(nameof(PeekColumns), defaultValue: 3);

    /// <summary>The row being peeked or open.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty = AvaloniaProperty.Register<RowList, object?>(nameof(SelectedItem));

    /// <summary>True while the page shows the peek pane beside the list.</summary>
    public static readonly StyledProperty<bool> IsPeekOpenProperty = AvaloniaProperty.Register<RowList, bool>(nameof(IsPeekOpen));

    /// <summary>The fact columns every row shows.</summary>
    public IReadOnlyList<RowColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>How many fact columns stay while the peek is open.</summary>
    public int PeekColumns
    {
        get => GetValue(PeekColumnsProperty);
        set => SetValue(PeekColumnsProperty, value);
    }

    /// <summary>The row being peeked or open.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>True while the page shows the peek pane beside the list.</summary>
    public bool IsPeekOpen
    {
        get => GetValue(IsPeekOpenProperty);
        set => SetValue(IsPeekOpenProperty, value);
    }

    /// <summary>The group whose header the sticky header shows now; null while the list is at its top.</summary>
    internal object? StickyGroup => throw new NotSupportedException("RowList.StickyGroup is not built yet (#2978).");

    /// <summary>How far the sticky header is pushed up by the next group's header.</summary>
    internal double StickyPush => throw new NotSupportedException("RowList.StickyPush is not built yet (#2978).");

    /// <summary>How many row containers exist now (group headers not counted).</summary>
    internal int RealizedRowCount => throw new NotSupportedException("RowList.RealizedRowCount is not built yet (#2978).");

    /// <summary>How many row containers were ever made, counting recycled ones once.</summary>
    internal int ContainersCreated => throw new NotSupportedException("RowList.ContainersCreated is not built yet (#2978).");

    /// <summary>Raised when the cursor row should open or close its peek.</summary>
    public event EventHandler<RowRequestEventArgs>? PeekRequested;

    /// <summary>Raised when the cursor row should open in full.</summary>
    public event EventHandler<RowRequestEventArgs>? OpenRequested;

    /// <summary>Raised when Esc should step back: close the peek first, then leave the full view.</summary>
    public event EventHandler<RowRequestEventArgs>? BackRequested;

    /// <summary>Scrolls the list to a vertical offset; the sticky header follows.</summary>
    /// <param name="offset">The vertical offset in px.</param>
    internal void ScrollTo(double offset) => throw new NotSupportedException("RowList.ScrollTo is not built yet (#2978).");

    internal void RaisePeek(RowRequestEventArgs e) => PeekRequested?.Invoke(this, e);

    internal void RaiseOpen(RowRequestEventArgs e) => OpenRequested?.Invoke(this, e);

    internal void RaiseBack(RowRequestEventArgs e) => BackRequested?.Invoke(this, e);
}
