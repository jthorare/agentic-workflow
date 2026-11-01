using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The Pages and actions section (pages spec §3.4, §3.6 "Keyboard"), bound to a <see cref="PagesSectionViewModel"/>: its host answers an action's name, Try again and close.</summary>
internal sealed partial class PagesSectionView : UserControl
{
    /// <summary>Builds the section.</summary>
    public PagesSectionView()
    {
        InitializeComponent();
        PagesAddSeveral.Content = Labels.AddSeveralPages;   // an internal const is no x:Static
        Avalonia.Automation.AutomationProperties.SetName(PagesAddSeveral, Labels.AddSeveralPages);
    }

    /// <summary>Raised from the Add several pages button and bubbles to the Context page, which opens the sheet.</summary>
    internal static readonly RoutedEvent<RoutedEventArgs> AddSeveralRequestedEvent = RoutedEvent.Register<PagesSectionView, RoutedEventArgs>(nameof(AddSeveralRequestedEvent), RoutingStrategies.Bubble);

    /// <summary>An action's name was pressed.</summary>
    internal event EventHandler<PageActionChosenEventArgs>? ActionChosen;

    /// <summary>Try again was pressed.</summary>
    internal event EventHandler? RetryRequested;

    /// <summary>The close button was pressed.</summary>
    internal event EventHandler? CloseRequested;

    /// <summary>Moves focus to the section's head.</summary>
    internal void FocusHead() => _ = PagesHead.Focus(NavigationMethod.Tab);

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PageActionRowViewModel row })
        {
            ActionChosen?.Invoke(this, new PageActionChosenEventArgs(row.Key));
        }
    }

    // Name this page or Edit page on a row; New page in the head. The host opens the sheet and gives focus back to the button that asked.
    private void OnRowSheet(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PageRowViewModel row } && DataContext is PagesSectionViewModel pages)
        {
            pages.OpenSheet(row);
        }
    }

    private void OnAddSeveral(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(AddSeveralRequestedEvent, this));

    private void OnAcceptAll(object? sender, RoutedEventArgs e) => (DataContext as PagesSectionViewModel)?.AcceptAll();

    private void OnNewPage(object? sender, RoutedEventArgs e) => (DataContext as PagesSectionViewModel)?.NewPage();

    // Try again reads the pages again; focus goes to the section head.
    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
        FocusHead();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    // Show all lists the rest; focus goes to the first newly shown row.
    private void OnShowAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PagesSectionViewModel pages)
        {
            return;
        }

        pages.ShowAll();
        Dispatcher.UIThread.Post(() => _ = PagesRows.ContainerFromIndex(PagesSectionViewModel.Cap)?.GetVisualDescendants().OfType<Button>().FirstOrDefault()?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }
}
