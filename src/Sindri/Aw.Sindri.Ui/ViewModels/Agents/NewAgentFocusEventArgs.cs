namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>Raised when focus should move to a field.</summary>
internal sealed class NewAgentFocusEventArgs(NewAgentField field) : EventArgs
{
    /// <summary>The field.</summary>
    internal NewAgentField Field { get; } = field;
}
