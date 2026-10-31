using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Views.Context.Concepts;

/// <summary>The concept band: one card per concept, opened by a click or Enter.</summary>
internal sealed partial class ConceptLayerView : UserControl
{
    private ConceptLayerViewModel? _watched;

    public ConceptLayerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    private void Watch()
    {
        _watched?.FocusRequested -= OnFocusRequested;
        _watched = DataContext as ConceptLayerViewModel;
        _watched?.FocusRequested += OnFocusRequested;
    }

    // After the sheet closed and the cards were drawn again: the button that opened it, or the card's Edit pin that took the place of Pin this.
    private void OnFocusRequested(object? sender, PinFocusEventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = Target(e)?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    private Button? Target(PinFocusEventArgs e) => e.Target switch
    {
        PinFocus.AddSeveral => this.FindControl<Button>("AddSeveralButton"),
        PinFocus.NewPin => this.FindControl<Button>("NewPinButton"),
        PinFocus.PinThis => CardButton(e.CardId, "PinThis"),
        PinFocus.EditPin => CardButton(e.CardId, "EditPin"),
        _ => null,
    };

    private Button? CardButton(string? cardId, string automationId) => this.GetVisualDescendants().OfType<Button>()
        .FirstOrDefault(b => b.IsEffectivelyVisible && string.Equals(AutomationProperties.GetAutomationId(b), automationId, StringComparison.Ordinal)
            && b.DataContext is ConceptTileViewModel tile && string.Equals(tile.Id, cardId, StringComparison.Ordinal));

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
