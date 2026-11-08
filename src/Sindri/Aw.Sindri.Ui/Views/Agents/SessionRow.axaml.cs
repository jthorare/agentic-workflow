using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>One live session: it passes a title click and focus arriving up to the page as routed events.</summary>
internal sealed partial class SessionRow : UserControl
{
    /// <summary>A title click: open the session's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<SessionRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<SessionRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    public SessionRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    private SessionRowViewModel? _watched;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        _watched?.PropertyChanged -= OnRowChanged;
        base.OnDataContextChanged(e);
        _watched = DataContext as SessionRowViewModel;
        _watched?.PropertyChanged += OnRowChanged;
        Columns();
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SessionRowViewModel.IsCompact), StringComparison.Ordinal))
        {
            Columns();
        }
    }

    // The same grid, the hidden columns at zero width: Status, Agent and For are what is left.
    private void Columns() => SessionRowGrid.ColumnDefinitions = ColumnDefinitions.Parse(_watched is { IsCompact: true } ? AgentColumnsLayout.Compact : AgentColumnsLayout.Wide);

    /// <summary>The row's session ref.</summary>
    internal string Key => (DataContext as SessionRowViewModel)?.Ref ?? string.Empty;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));


    private void OnCopyFocus(object? sender, RoutedEventArgs e) => _ = (DataContext as SessionRowViewModel)?.Focus.Copy(CancellationToken.None);
}
