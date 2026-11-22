using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: WorkflowsPage.axaml places it, which the analyzer cannot see
/// <summary>The graph edit commands of the Workflows page; each click calls one method of <see cref="GraphEditViewModel"/>.</summary>
internal sealed partial class GraphEditBar : UserControl
{
    public GraphEditBar()
    {
        InitializeComponent();
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GraphEditViewModel edit)
        {
            await edit.BeginAdd(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnPickBlock(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GraphEditViewModel edit && sender is Control { DataContext: WorkflowBlock block })
        {
            await edit.AddStep(block, CancellationToken.None).ConfigureAwait(true);
            this.FindAncestorOfType<WorkflowsPage>()?.FocusEditorField();   // a new step's sheet opens on its first required field
        }
    }

    private async void OnPickTarget(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GraphEditViewModel edit && sender is Control { DataContext: ConnectTarget target })
        {
            await edit.ConnectTo(target.Id, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnConfirmRemove(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GraphEditViewModel edit)
        {
            await edit.ConfirmRemove(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnReload(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GraphEditViewModel edit)
        {
            await edit.Reload(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnConnect(object? sender, RoutedEventArgs e) => (DataContext as GraphEditViewModel)?.BeginConnect();

    private void OnRemove(object? sender, RoutedEventArgs e) => (DataContext as GraphEditViewModel)?.RequestRemove();

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as GraphEditViewModel)?.Cancel();
}
#pragma warning restore MA0182
