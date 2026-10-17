namespace Aw.Sindri.Core.Launch;

/// <summary>What the probe found about the session host (launcher L-4): 'launch.probed''s 'host'.</summary>
/// <param name="Kind">What owns the process.</param>
/// <param name="Present">True when it can start a session.</param>
/// <param name="Version">Its version ('3.4' for tmux), or null.</param>
/// <param name="Problem">Why it can't ('not-installed', 'too-old'), or null.</param>
public sealed record HostFacts(HostKind Kind, bool Present, string? Version, string? Problem);
