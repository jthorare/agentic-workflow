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

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ConceptTileViewModel tile })
        {
            tile.Open();
        }
    }
}
