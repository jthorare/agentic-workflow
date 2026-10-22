using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Recipes;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Recipes;

/// <summary>One workflow: a title click is passed up to the page as a routed event.</summary>
internal sealed partial class RecipeRow : UserControl
{
    /// <summary>A title click: open the workflow's sheet.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<RecipeRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    public RecipeRow()
    {
        InitializeComponent();
    }

    /// <summary>The row's workflow.</summary>
    internal RecipeRowViewModel? Recipe => DataContext as RecipeRowViewModel;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageLayout.TierProperty && this.FindControl<Grid>("RecipeRowGrid") is { } grid)
        {
            RecipeColumns.Apply(grid, change.GetNewValue<PageTier>());
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
