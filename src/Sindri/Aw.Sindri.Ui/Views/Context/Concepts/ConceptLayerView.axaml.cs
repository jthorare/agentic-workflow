using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Views.Context.Concepts;

/// <summary>The concept band: one card per concept, opened by a click or Enter.</summary>
internal sealed partial class ConceptLayerView : UserControl
{
    public ConceptLayerView()
    {
        InitializeComponent();
    }

    private void OnPinThis(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ConceptTileViewModel tile } && DataContext is ConceptLayerViewModel layer)
        {
            layer.PinThis(tile);
        }
    }

    private void OnEditPin(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ConceptTileViewModel tile } && DataContext is ConceptLayerViewModel layer)
        {
            layer.EditPin(tile);
        }
    }

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ConceptTileViewModel tile } && DataContext is ConceptLayerViewModel layer)
        {
            layer.RunAction(tile);
        }
    }

    private void OnNewPin(object? sender, RoutedEventArgs e) => (DataContext as ConceptLayerViewModel)?.NewPin();

    private void OnAddSeveral(object? sender, RoutedEventArgs e) => (DataContext as ConceptLayerViewModel)?.AddSeveral();

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ConceptTileViewModel tile })
        {
            tile.Open();
        }
    }
}
