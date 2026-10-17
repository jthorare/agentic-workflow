using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// One launch for <see cref="ILauncher"/> (API P10a; the shape engine P8 calls with, agreed 2026-10-16). The id is the engine's
/// 'LCH-…' <see cref="LaunchId"/>, not the collector's looser one of this namespace.
/// </summary>
/// <param name="Launch">The launch id; a second call with the same id returns the first call's session.</param>
/// <param name="Item">The item the session works on.</param>
/// <param name="Agent">The catalog entry to run.</param>
/// <param name="Mode">Open, attach or resume (the engine always passes Open).</param>
/// <param name="Attended">True when a person opened it (the viewer shows it); false for the engine's agent runtime (no viewer, tmux, approved argv only).</param>
/// <param name="RepoPath">The repo checkout the session starts in.</param>
/// <param name="Instance">The workflow instance an unattended launch belongs to, or null.</param>
/// <param name="Worktree">The engine-computed worktree '&lt;repo&gt;/.claude/worktrees/sindri-&lt;instance&gt;-&lt;run&gt;' (created by the launcher when missing), or null.</param>
/// <param name="Step">The Step's arguments; non-null only when <paramref name="Attended"/> is false.</param>
/// <param name="Run">The Step run's token, or null.</param>
public sealed record LaunchRequest(
    LaunchId Launch,
    ItemId Item,
    AgentId Agent,
    LaunchMode Mode,
    bool Attended,
    string RepoPath,
    InstanceAddress? Instance,
    string? Worktree,
    StepArgs? Step,
    RunToken? Run);
