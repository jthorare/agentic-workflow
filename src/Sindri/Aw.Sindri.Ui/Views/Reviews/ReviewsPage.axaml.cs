using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Views.Reviews;

/// <summary>The Reviews page: J and K move the focus to the next and previous PR row.</summary>
internal sealed partial class ReviewsPage : UserControl
{
    public ReviewsPage()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown);
        // why: ReviewsGroups is in this page's own XAML
        this.FindControl<ItemsControl>("ReviewsGroups")!.SizeChanged += (_, e) =>
        {
            _width = e.NewSize.Width - PanelBorder - RowPadding;   // why: the groups fill the page, so a sheet beside it narrows them
            Replan();
        };
    }

    private double _width;
    private ViewModels.Shell.ShellViewModel? _shell;

    // Grammar (#2978): with a sheet open the row keeps its first 3 facts; the plan carries the sheet state and the width.
    private void Replan() => Views.Layout.PageLayout.SetReviewsPlan(this, new Views.Layout.PageLayout.ReviewsPlan(_width, _shell is { IsSheetShown: true }));

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _shell = TopLevel.GetTopLevel(this)?.DataContext as ViewModels.Shell.ShellViewModel;
        _shell?.PropertyChanged += OnShellChanged;
        Replan();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _shell?.PropertyChanged -= OnShellChanged;
        _shell = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnShellChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ViewModels.Shell.ShellViewModel.IsSheetShown), StringComparison.Ordinal))
        {
            Replan();
        }
    }

    // The room the columns have: the group's width less the panel's 1 px border a side and the rows' 16 px padding a side.
    private const double PanelBorder = 2;
    private const double RowPadding = 32;

    private ReviewsPageViewModel? Page => DataContext as ReviewsPageViewModel;

    private void OnSaveLogin(object? sender, RoutedEventArgs e) => _ = Page?.SaveLogin(CancellationToken.None);

    private void OnConnect(object? sender, RoutedEventArgs e) => Page?.PressConnect();

    private void OnLoginKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Page is { CanSaveLogin: true } page)
        {
            _ = page.SaveLogin(CancellationToken.None);
            e.Handled = true;
        }
    }

    private void OnOpenLibrary(object? sender, RoutedEventArgs e) => _ = Page?.OpenLibrary(CancellationToken.None);

    // Enter on a PR row opens its review page.
    private bool OpenRowOnEnter(KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || e.Source is not Border { DataContext: ReviewRowViewModel row } || Page is not { } list)
        {
            return false;
        }

        list.OpenRow(row);
        e.Handled = true;
        return true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (OpenRowOnEnter(e))
        {
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
