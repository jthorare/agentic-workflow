namespace Aw.Sindri.Core.Launch;

/// <summary>Which tmux allowlist a call is checked against (agents spec §15.2).</summary>
public enum TmuxList
{
    /// <summary>A socket Sindri did not create: read and type only.</summary>
    Foreign = 0,

    /// <summary>The socket 'tmux -L sindri-launch': the foreign verbs plus start, resize, discovery and kill-session of a 'sindri-' name.</summary>
    Launch = 1,
}
