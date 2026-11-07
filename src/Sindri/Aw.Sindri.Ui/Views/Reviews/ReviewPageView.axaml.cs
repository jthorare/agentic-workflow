using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Views.Reviews;

/// <summary>
/// One PR's review page: the two layouts by window width (the panel column 420 px beside the diff from 1,280 px, else stacked above it), the diff keys (§6.4), the buttons
/// that open GitHub, and focus moving where the view model says.
/// </summary>
internal sealed partial class ReviewPageView : UserControl
{
    private const double WideFrom = 1280;
    private IDisposable? _width;

    public ReviewPageView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown);
        DataContextChanged += (_, _) =>
        {
            if (Page is { } page)
            {
                page.PropertyChanged += (_, e) =>
                {
                    if (string.Equals(e.PropertyName, nameof(ReviewPageViewModel.Focus), StringComparison.Ordinal))
                    {
                        Dispatcher.UIThread.Post(MoveFocus, DispatcherPriority.Loaded);
                    }
                };
                Layout();
            }
        };
    }

    private ReviewPageViewModel? Page => DataContext as ReviewPageViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _width = TopLevel.GetTopLevel(this)?.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new WidthObserver(this));
        Layout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _width?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    // The layout follows the window's width, not the page's: a sheet beside the page narrows the page, not the layout.
    private void Layout()
    {
        var wide = (TopLevel.GetTopLevel(this)?.ClientSize.Width ?? 0) >= WideFrom;
        if (Page is { } page)
        {
            page.IsWide = wide;
        }

        LayoutBody(wide);
    }

    private void LayoutBody(bool wide)
    {
        if (this.FindControl<Grid>("ReviewBody") is not { } body || this.FindControl<Control>("ReviewPanelColumn") is not { } column || this.FindControl<Control>("ReviewDiffPane") is not { } diff)
        {
            return;
        }

        body.ColumnDefinitions = wide ? new ColumnDefinitions("420,*") : new ColumnDefinitions("*");
        body.RowDefinitions = wide ? new RowDefinitions("Auto") : new RowDefinitions("Auto,Auto");
        Grid.SetColumn(diff, wide ? 1 : 0);
        Grid.SetRow(diff, wide ? 0 : 1);
        diff.Margin = wide ? new Thickness(16, 0, 0, 0) : default;
        column.Width = wide ? 420 : double.NaN;
    }

    private sealed class WidthObserver(ReviewPageView view) : IObserver<Size>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value) => view.Layout();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Page is not { } page || e.KeyModifiers != KeyModifiers.None || KeyWord(e.Key) is not { } word)
        {
            return;
        }

        var inDiff = this.FindControl<Control>("ReviewDiffPane")?.IsKeyboardFocusWithin == true;
        e.Handled = page.PressDiffKey(word, inDiff, e.Source is TextBox);
    }

#pragma warning disable IDE0072 // every other key is not the page's
    private static string? KeyWord(Key key) => key switch
    {
        Key.J => "j",
        Key.K => "k",
        Key.N => "n",
        Key.P => "p",
        Key.O => "o",
        Key.C => "c",
        Key.Enter => "Enter",
        _ => null,
    };
#pragma warning restore IDE0072

    private void MoveFocus()
    {
        if (Page?.Focus is not { } focus)
        {
            return;
        }

        var all = this.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        _ = Find(all, focus)?.Focus(NavigationMethod.Tab);
    }

    private static Control? Find(List<Control> all, ReviewFocus focus) => focus.Region switch
    {
        ReviewFocusRegion.StripFirst => all.FirstOrDefault(c => c.Name == "ReviewStripSegment" && c.IsEnabled) ?? all.FirstOrDefault(c => c.Name == "ReviewTitle"),
        ReviewFocusRegion.Panel => PanelTarget(all, focus.Key),
        ReviewFocusRegion.DiffLine => all.FirstOrDefault(c => c.Name == "ReviewDiffLine" && string.Equals(c.Tag as string, focus.Key, StringComparison.Ordinal)),
        ReviewFocusRegion.StateHeading => all.FirstOrDefault(c => c.Name == "ReviewStateHeading"),
        ReviewFocusRegion.FileNote => all.FirstOrDefault(c => c.Name == "ReviewDiffFile" && string.Equals(c.Tag as string, focus.Key, StringComparison.Ordinal)),
        ReviewFocusRegion.GroupHeader => all.FirstOrDefault(c => c.Name == "ReviewDiffGroupHeader" && string.Equals(c.Tag as string, focus.Key, StringComparison.Ordinal)),
        ReviewFocusRegion.None or ReviewFocusRegion.FixText or ReviewFocusRegion.FixOutcome => null,
        _ => null,
    };

    private static Control? PanelTarget(List<Control> all, string key)
    {
        var row = key.Split(':') is [_, var id] ? id : null;
        return row is null
            ? all.FirstOrDefault(c => c.Name == "ReviewPanelHeader" && string.Equals(c.Tag as string, key, StringComparison.Ordinal))
            : all.FirstOrDefault(c => (string.Equals(c.Name, "ReviewCheckRow", StringComparison.Ordinal) || string.Equals(c.Name, "ReviewDocRow", StringComparison.Ordinal)) && string.Equals(c.Tag as string, row, StringComparison.Ordinal));
    }

    private static void Launch(Control from, string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            _ = TopLevel.GetTopLevel(from)?.Launcher.LaunchUriAsync(uri);
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e) => Page?.GoBack();

    private void OnTryAgain(object? sender, RoutedEventArgs e) => _ = Page?.Reload(CancellationToken.None);

    private void OnSaveLogin(object? sender, RoutedEventArgs e) => _ = Page?.SaveLogin(CancellationToken.None);

    private void OnLoginKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Page is { Login.CanSave: true } page)
        {
            _ = page.SaveLogin(CancellationToken.None);
            e.Handled = true;
        }
    }

    private void OnOpenGitHub(object? sender, RoutedEventArgs e) => Launch(this, Page?.PrUrl);

    private void OnOpenReviewBox(object? sender, RoutedEventArgs e) => Launch(this, Page?.ReviewUrl);

    private void OnStripSegment(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewStripSegmentViewModel segment })
        {
            Page?.Jump(segment.Panel);
        }
    }

    private void OnTogglePanel(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewPanelViewModel panel })
        {
            panel.IsCollapsed = !panel.IsCollapsed;
        }
    }

    private void OnCheckFix(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewCheckRowViewModel check })
        {
            Page?.RequestCheckFix(check.Id);
        }
    }

    private void OnRequirementFix(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewDocItemViewModel item })
        {
            Page?.RequestRequirementFix(item.Id);
        }
    }

    private void OnToggleGroup(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewDiffGroupViewModel group })
        {
            group.IsFolded = !group.IsFolded;
        }
    }

    private void OnShowFile(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewDiffFileViewModel file })
        {
            file.Show();
        }
    }

    private void OnOpenFile(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewDiffFileViewModel file })
        {
            Launch(this, file.Url);
        }
    }

    private void OnLineFocused(object? sender, FocusChangedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewDiffLineViewModel line })
        {
            Page?.Diff.FocusLine(line);
            Page?.FocusedFrom(ReviewFocusRegion.DiffLine, line.Key);
        }
    }

    private void OnGroupHeaderFocused(object? sender, FocusChangedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewDiffGroupViewModel group })
        {
            Page?.FocusedFrom(ReviewFocusRegion.GroupHeader, group.Name);
        }
    }
}
