using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The automation peer of <see cref="CommandControl"/>: invoking it presses the control as its button does, so the UI driver (and a screen reader) can act on the control itself.</summary>
/// <param name="owner">The control.</param>
internal sealed class CommandControlAutomationPeer(CommandControl owner) : ContentControlAutomationPeer(owner), IInvokeProvider
{
    private readonly CommandControl _owner = owner;

    /// <inheritdoc />
    public void Invoke() => _owner.Invoke();

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
}
