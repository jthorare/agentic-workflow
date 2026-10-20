using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.Views.Jobs;

/// <summary>One job: a title click is passed up to the page as a routed event.</summary>
internal sealed partial class JobRow : UserControl
{
    /// <summary>A title click: open the job's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<JobRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    public JobRow()
    {
        InitializeComponent();
    }

    /// <summary>The row's job.</summary>
    internal JobRowViewModel? Job => DataContext as JobRowViewModel;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
