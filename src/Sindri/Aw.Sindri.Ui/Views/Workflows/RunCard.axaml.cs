using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One run on the board: focus arriving selects it and a double click opens it, both passed up to the page as the list rows' routed events.</summary>
internal sealed partial class RunCard : UserControl
{
    public RunCard()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(InstanceRow.SelectEvent, this)));
        AddHandler(DoubleTappedEvent, (_, _) => RaiseEvent(new RoutedEventArgs(InstanceRow.OpenEvent, this)));
    }

    /// <summary>The run's instance address.</summary>
    internal string Address => (DataContext as InstanceRowViewModel)?.Address ?? string.Empty;
}
