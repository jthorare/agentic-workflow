namespace Aw.Sindri.Core.Home;

/// <summary>One lane's context ('aw-meta lanes-context --json' lanes[], with its tmux session name from 'status --json' lanes.entries[]).</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="SessionName">The lane's tmux session name ('sindri-v3-ui'), or null.</param>
/// <param name="Session">The agent session id, or null.</param>
/// <param name="Tokens">The context size in tokens, or null when unknown.</param>
/// <param name="Threshold">The lane's recycle threshold in tokens.</param>
/// <param name="Verdict">'Ok', 'Recycle' or 'Unknown'.</param>
public sealed record MetaLane(string Lane, string? SessionName, string? Session, long? Tokens, long Threshold, string Verdict);
