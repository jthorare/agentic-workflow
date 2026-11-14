using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The mouse path of <see cref="RowList"/> (#2978).</summary>
internal sealed partial class RowList
{
    private bool _cursorBeforePress;
    private int _clickCount;

    // Mouse path. A click puts the cursor on the row (focus does it, and an open peek follows); a click on the row that already had the cursor toggles the peek, like a Space tap;
    // a double-click opens the row in full. A click on a button in the row is the button's.
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _cursorBeforePress = RowOf(e.Source) is { IsKeyboardFocusWithin: true };
        _clickCount = e.ClickCount;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (RowOf(e.Source) is not { } row || IsInButton(e.Source))
        {
            return;
        }

        if (!row.IsKeyboardFocusWithin)
        {
            _ = row.Focus(NavigationMethod.Pointer);
        }
        else if (_cursorBeforePress && _clickCount < 2 && IndexFromContainer(row) is var index and >= 0)
        {
            RaisePeek(new RowRequestEventArgs { Item = ItemAt(index) });
        }

        e.Handled = true;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RowOf(e.Source) is { } row && !IsInButton(e.Source) && IndexFromContainer(row) is var index and >= 0)
        {
            RaiseOpen(new RowRequestEventArgs { Item = ItemAt(index) });
            e.Handled = true;
        }
    }

    private static Row? RowOf(object? source) => (source as Visual)?.FindAncestorOfType<Row>(includeSelf: true);

    private static bool IsInButton(object? source) => (source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null;
}
