namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The columns of the Agents table that a header click sorts by.</summary>
internal enum AgentSortColumn
{
    /// <summary>The status: the sessions that wait on you first.</summary>
    Status,

    /// <summary>The agent's title.</summary>
    Agent,

    /// <summary>The repository.</summary>
    Repo,

    /// <summary>The host and lane.</summary>
    Where,

    /// <summary>What it asks or runs.</summary>
    Doing,

    /// <summary>How long it has been in its status; the shortest first.</summary>
    For,
}
