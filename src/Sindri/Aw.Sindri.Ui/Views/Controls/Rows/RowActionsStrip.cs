using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The action buttons of a hot row (#3189): a Surface2 box with 8 px of padding that covers the age and waits-on text at the right end. A flat row has no child controls until it is hot
/// (pointer over it, or keyboard focus within it); then it takes one strip from this pool, and gives it back when it goes cold or is recycled. The pool grows on demand.
/// </summary>
internal sealed class RowActionsStrip : Border
{
    private static readonly Stack<RowActionsStrip> _pool = new();

    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal };

    public RowActionsStrip()
    {
        Padding = new Thickness(8, 0, 0, 0);
        Child = _buttons;
        _ = Bind(BackgroundProperty, this.GetResourceObservable("Brush.Surface2"));
    }

    /// <summary>Takes a strip from the pool, or makes one.</summary>
    /// <returns>A strip with no actions.</returns>
    internal static RowActionsStrip Rent() => _pool.Count > 0 ? _pool.Pop() : new RowActionsStrip();

    /// <summary>Gives a strip back to the pool.</summary>
    /// <param name="strip">The strip a row no longer shows.</param>
    internal static void Return(RowActionsStrip strip)
    {
        ArgumentNullException.ThrowIfNull(strip);
        strip.Fill([]);
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
                _buttons.Children.Add(new Button { Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 0), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center });
            }

            var button = (Button)_buttons.Children[i];
            button.Content = actions[i].Label;
            button.Command = actions[i].Command;
        }
    }
}
