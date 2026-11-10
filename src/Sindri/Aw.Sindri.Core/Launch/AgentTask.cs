using Aw.Sindri.Core.Sandbox.Resources;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What a graph node's session is asked to do (PR 7): the first message it gets, the output fields it must report, the sandbox host groups the node adds and the model it asks for.
/// Beside <see cref="StepArgs"/> (which carries the policy and the brief the launcher renders), so a node launch is told apart from an attended open.
/// </summary>
/// <param name="Brief">The first message: the interpolated prompt, the output list and the exact report command.</param>
/// <param name="Outputs">The declared output fields by name, each with its type ('string', 'number', 'boolean', 'object' or 'array'; a trailing '?' marks an optional one).</param>
/// <param name="HostGroups">The sandbox host groups the node adds to the session's own: 'github' for a node that names a repo, and the groups of the connections it names.</param>
/// <param name="Model">The model the node asks for, or null for the agent's default.</param>
/// <param name="ResourceMounts">The workflow's resource folders the session may write (PR 14, #2532); null when it declares none.</param>
/// <param name="ResourceLimits">The workflow's own per-run sandbox limits (PR 14, #2532), clamped later by the plan; null when it declares none.</param>
public sealed record AgentTask(string Brief, IReadOnlyDictionary<string, string> Outputs, IReadOnlyList<string> HostGroups, string? Model = null, IReadOnlyList<ResourceMount>? ResourceMounts = null, ResourceLimitOverride? ResourceLimits = null);
