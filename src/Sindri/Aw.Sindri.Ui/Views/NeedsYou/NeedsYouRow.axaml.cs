using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>One Needs-you row: it passes a title click, a control press and focus arriving up to Home as routed events.</summary>
internal sealed partial class NeedsYouRow : UserControl
{
    /// <summary>A title click: open the row's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<NeedsYouRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<NeedsYouRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    /// <summary>A control was pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActionEvent = RoutedEvent.Register<NeedsYouRow, RoutedEventArgs>(nameof(ActionEvent), RoutingStrategies.Bubble);

    public NeedsYouRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>The row's id.</summary>
    internal string RowId => (DataContext as NeedsYouRowViewModel)?.Id ?? string.Empty;

    /// <summary>The action of the control an <see cref="ActionEvent"/> came from, or null.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The action.</returns>
    internal static NeedsYouAction? ActionOf(RoutedEventArgs e) => e is RowActionEventArgs { Action: var action } ? action : null;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));

    private void OnSkillDetails(object? sender, RoutedEventArgs e) => (DataContext as NeedsYouRowViewModel)?.ToggleSkillDetails();

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        // RaiseEvent makes the row the event's source, so the pressed control's action travels in the args.
        if (sender is Control { DataContext: NeedsYouActionControl { Action: var action } })
        {
            RaiseEvent(new RowActionEventArgs(action));
        }
    }
}
