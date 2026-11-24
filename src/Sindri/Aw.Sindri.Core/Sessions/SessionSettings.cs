using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>The collector's settings (collector spec §9): launcher.json's 'sessions' key and the test overrides, read once at start.</summary>
/// <param name="Enabled">False turns the whole collector off (SND-SESSION-002 'disabled').</param>
/// <param name="PollSeconds">The pass cadence, 1 to 30.</param>
/// <param name="Providers">The agents to discover; null means every catalog entry.</param>
/// <param name="TestOverride">True when AW_SESSIONS_PROC_ROOT or AW_SESSIONS_CLAUDE_DIR is set: pruning and focus are off and one informational problem shows.</param>
public sealed record SessionSettings(bool Enabled, int PollSeconds, IReadOnlyList<AgentId>? Providers, bool TestOverride)
{
    /// <summary>The settings of a box with no launcher.json.</summary>
    public static SessionSettings Defaults { get; } = new(Enabled: true, PollSeconds: 2, Providers: null, TestOverride: false);

    /// <summary>A launcher.json problem to show as a problem row, or null when the file read.</summary>
    public string? ConfigProblem { get; init; }

    /// <summary>Which sessions discovery lists; <see cref="SessionScope.All"/> unless launcher.json says 'own'.</summary>
    public SessionScope Scope { get; init; }

    /// <summary>This home's own tmux directory (the home's 'tmux' folder, where a scratch home points TMUX_TMPDIR); under scope 'own' only sockets below it are Sindri's.</summary>
    public string? OwnTmuxDirectory { get; init; }
}
