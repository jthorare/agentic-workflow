using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Recipes;

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

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
