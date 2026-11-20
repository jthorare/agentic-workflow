namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>Raised when the form wants a session's terminal view opened.</summary>
internal sealed class AgentOpenRequestedEventArgs(AgentStartedViewRef started) : EventArgs
{
    /// <summary>The session.</summary>
    internal AgentStartedViewRef Started { get; } = started;
}
