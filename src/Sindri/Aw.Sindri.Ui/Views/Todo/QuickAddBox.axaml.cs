using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>The quick-add box's view (todo spec §3.2): keys only. Enter saves and Esc closes, caught before the field sees them. Where focus goes when it opens and closes is the window's.</summary>
internal sealed partial class QuickAddBox : UserControl
{
    public QuickAddBox()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    /// <summary>Moves focus into the field (the window calls it when the box opens).</summary>
    internal void FocusField() => QuickAddField.Focus(NavigationMethod.Tab);

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickAddViewModel box)
        {
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            _ = box.Save(CancellationToken.None);
        }
        else if (e.Key == Key.Escape)
        {
            box.Close();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }
}
