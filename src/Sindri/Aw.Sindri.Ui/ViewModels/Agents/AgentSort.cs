namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>How the rows of a group are ordered (#3082): the default, or the user's pick, which holds across reads.</summary>
internal enum AgentSort
{
    /// <summary>The default: by name.</summary>
    Name,

    /// <summary>By repo, then name.</summary>
    Repo,

    /// <summary>By state (asking, working, idle, unknown), then name.</summary>
    State,

    /// <summary>By age, the longest in its state first, then name.</summary>
    Age,
}
