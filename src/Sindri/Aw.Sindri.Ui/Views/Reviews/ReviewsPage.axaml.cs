using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Reviews;

/// <summary>The Reviews page: J and K move the focus to the next and previous PR row.</summary>
internal sealed partial class ReviewsPage : UserControl
{
    public ReviewsPage()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.J or Key.K) || e.Source is TextBox)
        {
            return;
        }

        var rows = this.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("review-row") && b.IsEffectivelyVisible).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var at = rows.FindIndex(r => r.IsKeyboardFocusWithin);
        var next = e.Key == Key.J ? Math.Min(at + 1, rows.Count - 1) : Math.Max(at - 1, 0);
        _ = rows[next].Focus(NavigationMethod.Tab);
        e.Handled = true;
    }
}
