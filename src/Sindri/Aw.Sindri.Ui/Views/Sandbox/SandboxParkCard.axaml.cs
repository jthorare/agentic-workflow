using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Views.Sandbox;

/// <summary>The park card on a Needs-you row whose Step can't start because of the sandbox (sandbox spec 8); binds to <see cref="SandboxParkCardViewModel"/>.</summary>
internal sealed partial class SandboxParkCard : UserControl
{
    public SandboxParkCard()
    {
        InitializeComponent();
    }

    /// <summary>Puts focus on the card's heading.</summary>
    internal void FocusHeading() => _ = SandboxParkHeading.Focus(NavigationMethod.Directional);

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SandboxParkCardViewModel card)
        {
            _ = card.Check.Copy(card.Row, CancellationToken.None);
        }
    }
}
