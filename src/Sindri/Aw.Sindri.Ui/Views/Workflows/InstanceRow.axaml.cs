using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One row of the Workflows list: it passes a title click and focus arriving up to the page as routed events, and keeps the columns the list gives it.</summary>
internal sealed partial class InstanceRow : UserControl
{
    /// <summary>A title click: open the instance.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<InstanceRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<InstanceRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    public InstanceRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>The row's instance address.</summary>
    internal string Address => (DataContext as InstanceRowViewModel)?.Address ?? string.Empty;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Fit(GetValue(ListBands.ColumnsProperty));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ListBands.ColumnsProperty)
        {
            Fit(change.GetNewValue<ListColumns>());
        }
    }

    // The list narrowed or widened: the columns it keeps are the row's (the mono reference under the title goes at the narrowest).
    private void Fit(ListColumns columns)
    {
        if (this.FindControl<Grid>("WorkflowRowGrid") is { } grid)
        {
            ListBands.Apply(grid, columns);
        }

        this.FindControl<Control>("WorkflowRowReference")?.IsVisible = columns.ShowsReference;
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
