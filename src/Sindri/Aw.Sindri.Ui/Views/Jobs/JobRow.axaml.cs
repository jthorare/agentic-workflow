using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.Views.Jobs;

/// <summary>One job: a title click is passed up to the page as a routed event; under 760 px its grid keeps three columns.</summary>
internal sealed partial class JobRow : UserControl
{
    /// <summary>A title click: open the job's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<JobRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    private JobRowViewModel? _watched;

    public JobRow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    /// <summary>The row's job.</summary>
    internal JobRowViewModel? Job => DataContext as JobRowViewModel;

    private void Watch()
    {
        if (_watched is { } old)
        {
            old.PropertyChanged -= OnChanged;
        }

        _watched = Job;
        if (_watched is { } now)
        {
            now.PropertyChanged += OnChanged;
        }

        Columns();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(JobRowViewModel.IsCompact), StringComparison.Ordinal))
        {
            Columns();
        }
    }

    // The same grid, the hidden columns at zero width: Job, Status, Rounds and Cancel are what is left.
    private void Columns() =>
        JobRowGrid.ColumnDefinitions = ColumnDefinitions.Parse(Job is { IsCompact: true } ? JobsColumns.Compact : JobsColumns.Wide);

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
