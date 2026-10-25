using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// One action a child row serves, other than opening its item. The app sends none of them yet (the send is U8b): an action the catalog
/// classes as protected is the lock button with its popover (a child row serves no CLI, so the popover says so and the child's own frame has the command),
/// and the rest show a chip that says the send arrives later.
/// </summary>
internal sealed class ChildActionViewModel
{
    internal ChildActionViewModel(string action, string address, IClipboard clipboard, TimeProvider clock, CommandRowView? served)
    {
        Label = Labels.Action(action);
        Protected = served is not null && string.Equals(served.Class, "protected", StringComparison.Ordinal) ? new ProtectedActionViewModel(new ProtectedAction(Label, Cli: string.Empty, address), clipboard, clock) : null;
        PendingChip = Protected is not null ? string.Empty : Labels.AvailableWithU8b;
        Name = Labels.ChildActionName(Label, address, PendingChip);
    }

    /// <summary>The action's words.</summary>
    public string Label { get; }

    /// <summary>The control's accessible name: the words and the child.</summary>
    public string Name { get; }

    /// <summary>The lock button for a protected action; null for the others.</summary>
    public ProtectedActionViewModel? Protected { get; }

    /// <summary>The chip that says the send arrives later; empty for a protected action.</summary>
    public string PendingChip { get; }

    /// <summary>True when the action shows as a chip.</summary>
    public bool IsPending => Protected is null;
}
