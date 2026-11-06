using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Reviews;
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

    private ReviewsPageViewModel? Page => DataContext as ReviewsPageViewModel;

    private void OnSaveLogin(object? sender, RoutedEventArgs e) => _ = Page?.SaveLogin(CancellationToken.None);

    private void OnLoginKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Page is { CanSaveLogin: true } page)
        {
            _ = page.SaveLogin(CancellationToken.None);
            e.Handled = true;
        }
    }

    private void OnOpenLibrary(object? sender, RoutedEventArgs e) => _ = Page?.OpenLibrary(CancellationToken.None);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && e.Source is Border { DataContext: ReviewRowViewModel row } && Page is { } list)
        {
            list.OpenRow(row);   // Enter on a PR row opens its review page
            e.Handled = true;
            return;
        }

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
