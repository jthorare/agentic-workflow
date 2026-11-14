namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The fields of the New agent form in Tab order (agents spec §15.4).</summary>
internal enum NewAgentField
{
    /// <summary>The prompt; focus starts here.</summary>
    Prompt,

    /// <summary>The repo choice.</summary>
    Repo,

    /// <summary>The agent choice.</summary>
    Agent,

    /// <summary>The New worktree switch.</summary>
    Worktree,

    /// <summary>The Start button.</summary>
    Start,
}
