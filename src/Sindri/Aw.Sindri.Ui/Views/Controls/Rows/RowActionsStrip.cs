using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The action buttons of a hot row (#3189): a Surface2 box with 8 px of padding and a 168 px minimum width that covers the age and waits-on text at the right end. A flat row has no child controls until it is hot
/// (pointer over it, or keyboard focus within it); then it takes one strip from this pool, and gives it back when it goes cold or is recycled. The pool grows on demand.
/// </summary>
internal sealed class RowActionsStrip : Border
{
    private static readonly Stack<RowActionsStrip> _pool = new();
    private static long _made;

    // grammar.css: min-width col-age + col-wait + sp3 (44 + 112 + 12 = 168), so the age and the waits-on text sit under the strip whole.
    private const double StripMinWidth = 168;

    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 4 };

    public RowActionsStrip()
    {
        _ = Interlocked.Increment(ref _made);
        Padding = new Thickness(8, 0, 0, 0);
        Child = _buttons;
        MinWidth = StripMinWidth;
        _ = Bind(BackgroundProperty, this.GetResourceObservable("Brush.Surface2"));
    }

    /// <summary>How many strips were ever made. Rows share a few, so scrolling past thousands of rows does not move it.</summary>
    internal static long Made => Interlocked.Read(ref _made);

    /// <summary>Takes a strip from the pool, or makes one.</summary>
    /// <returns>A strip with no actions.</returns>
    internal static RowActionsStrip Rent() => _pool.Count > 0 ? _pool.Pop() : new RowActionsStrip();

    /// <summary>Gives a strip back to the pool. Its buttons stay, with no command and no label, so the next row that rents it fills them in place.</summary>
    /// <param name="strip">The strip a row no longer shows.</param>
    internal static void Return(RowActionsStrip strip)
    {
        ArgumentNullException.ThrowIfNull(strip);
        foreach (var button in strip._buttons.Children.OfType<Button>())
        {
            button.Command = null;   // the buttons stay (a hover would otherwise build new templated ones); only what they run and say is dropped
            button.Content = null;
        }

        _pool.Push(strip);
    }

    /// <summary>Shows these actions: a button each, reusing the buttons the strip already has.</summary>
    /// <param name="actions">The item's actions.</param>
    internal void Fill(IReadOnlyList<RowAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        while (_buttons.Children.Count > actions.Count)
        {
            _buttons.Children.RemoveAt(_buttons.Children.Count - 1);
        }

        for (var i = 0; i < actions.Count; i++)
        {
            if (i >= _buttons.Children.Count)
            {
                _buttons.Children.Add(new Button { Padding = new Thickness(8, 0), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center });
            }

            var button = (Button)_buttons.Children[i];
            button.Content = actions[i].Label;
            button.Command = actions[i].Command;
        }
    }
}
