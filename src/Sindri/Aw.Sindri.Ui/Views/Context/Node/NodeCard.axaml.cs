using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Views.Context.Node;

/// <summary>The inspector's node card: bound to a <see cref="NodeCardViewModel"/>; Accept, Reject and Copy path call it.</summary>
internal sealed partial class NodeCard : UserControl
{
    public NodeCard()
    {
        InitializeComponent();
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => _ = (DataContext as NodeCardViewModel)?.Accept();

    private void OnReject(object? sender, RoutedEventArgs e) => _ = (DataContext as NodeCardViewModel)?.Reject();

    private void OnCopyPath(object? sender, RoutedEventArgs e) => _ = (DataContext as NodeCardViewModel)?.CopyPath();
}
