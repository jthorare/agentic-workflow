namespace Aw.Sindri.Core.Launch;

/// <summary>A started session in its host (launcher spec §3): what Stop, the attach argv and the late-attach poll address.</summary>
/// <param name="Kind">What owns the process.</param>
/// <param name="Name">The host's name for the session ('snd-&lt;launchId&gt;' for tmux).</param>
public sealed record HostRef(HostKind Kind, string Name);
