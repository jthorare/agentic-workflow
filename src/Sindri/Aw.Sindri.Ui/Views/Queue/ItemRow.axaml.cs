using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Queue;

/// <summary>One item row; a click on its title asks the page to open the item's sheet.</summary>
internal sealed partial class ItemRow : UserControl
{
    /// <summary>Raised when the row asks to open its item's sheet (a title click).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent =
        RoutedEvent.Register<ItemRow, RoutedEventArgs>(nameof(Open), RoutingStrategies.Bubble);

    /// <summary>Raised when focus arrives on the row or anything in it: the row is then the selected one (§3.3).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent =
        RoutedEvent.Register<ItemRow, RoutedEventArgs>("Select", RoutingStrategies.Bubble);

    public ItemRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>Raised when the row asks to open its item's sheet.</summary>
    public event EventHandler<RoutedEventArgs> Open
    {
        add => AddHandler(OpenEvent, value);
        remove => RemoveHandler(OpenEvent, value);
    }

    /// <summary>The item id of the row a routed event came from (a table or compact row), or empty.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The id, or empty.</returns>
    internal static string ItemIdOf(RoutedEventArgs e) => ((e.Source as Control)?.DataContext as ItemRowViewModel)?.Id ?? string.Empty;

    /// <summary>The row's item id, or empty.</summary>
    internal string ItemId => (DataContext as ItemRowViewModel)?.Id ?? string.Empty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == PageLayout.TierProperty || change.Property == PageLayout.HasRepoColumnProperty) && this.FindControl<Grid>("TableRow") is { } table)
        {
            PageLayout.Apply(table, PageLayout.GetTier(this), PageLayout.GetHasRepoColumn(this));
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
