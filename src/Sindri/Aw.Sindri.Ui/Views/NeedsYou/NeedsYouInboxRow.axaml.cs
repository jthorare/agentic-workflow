using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>One row of the Needs-you inbox: a click or focus on it selects it, which the page tells the list.</summary>
internal sealed partial class NeedsYouInboxRow : UserControl
{
    /// <summary>The row was clicked or took focus: it is the selected one.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<NeedsYouInboxRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    public NeedsYouInboxRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Bubble);
    }

    /// <summary>The row's id.</summary>
    internal string RowId => (DataContext as NeedsYouRowViewModel)?.Id ?? string.Empty;

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = InboxRowFrame.Focus();   // takes the focus: the row's own selection follows it
        RaiseEvent(new RoutedEventArgs(SelectEvent, this));
    }
}
