namespace Aw.Sindri.Core.Sessions;

/// <summary>The merge gate's result for a PR in the ready queue, as <c lang="text">aw-meta status --json</c> <c lang="text">mergeQueue[]</c> reports it (Agents page spec §3.2).</summary>
public enum AgentGate
{
    /// <summary>The gate has not run.</summary>
    None,

    /// <summary>The gate passed.</summary>
    Pass,

    /// <summary>The gate failed.</summary>
    Fail,

    /// <summary>The PR conflicts with main.</summary>
    Conflict,
}
