using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What <see cref="HostGroups"/> resolves from.</summary>
/// <param name="Settings">The profile's sandbox section, or null.</param>
/// <param name="Repo">The repo's declaration, or null.</param>
/// <param name="Target">The agent.</param>
/// <param name="WriteScope">True when the session may write (it gets 'packages').</param>
/// <param name="Attended">True for an attended session (never 'github').</param>
/// <param name="Github">The resolved GitHub credentials.</param>
/// <param name="Omit">Groups the Step removes from itself.</param>
/// <param name="NodeRepo">The repo of a 'run_agent' node, or null for a launch that is not a node. A node with a repo always gets 'github'.</param>
/// <param name="Connections">The connections the node names.</param>
/// <param name="ConnectionHosts">The hosts each connection's preset opens, by connection name. A connection with no entry (no preset yet, before PR 10) adds nothing.</param>
public sealed record GroupsInput(SandboxSettings? Settings, SandboxRepoSettings? Repo, AgentId Target, bool WriteScope, bool Attended, GithubCredentials Github, IReadOnlyList<string>? Omit,
    string? NodeRepo = null,
    IReadOnlyList<string>? Connections = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ConnectionHosts = null);
