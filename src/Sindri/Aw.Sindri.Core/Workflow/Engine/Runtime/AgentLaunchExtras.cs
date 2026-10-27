using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a Step's launch is given beside its run, target and descriptor.</summary>
/// <param name="Instructions">What the Step is told to do, for the brief; none when it has none.</param>
/// <param name="Workspace">The workspace the Step reads (an existing worktree it works in); null when it reads none.</param>
/// <param name="Bundle">The pack the Step bundles (LN-WF5), resolved; null for none.</param>
/// <param name="Facts">The Step's declared read facts as files for its run directory, file name to content (written by the launcher); null for none.</param>
/// <param name="Model">The model the Step's 'model' parameter asked for; null runs the agent's default.</param>
public sealed record AgentLaunchExtras(IReadOnlyList<string>? Instructions = null, string? Workspace = null, PackBundle? Bundle = null, IReadOnlyDictionary<string, string>? Facts = null, string? Model = null);
