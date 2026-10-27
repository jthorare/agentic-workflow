namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>How an agent Step's session ended, as the watch reports it (engine P8 D3).</summary>
public enum AgentSessionEnd
{
    /// <summary>The agent exited and 'session wrap' reported it.</summary>
    Exit = 1,

    /// <summary>No heartbeat for 120 s.</summary>
    Lost = 2,

    /// <summary>The agent sat idle at its prompt with no report: its turn ended and nothing happened for 120 s, or an API error stopped it and its one re-prompt got no answer.</summary>
    Idle = 3,
}
