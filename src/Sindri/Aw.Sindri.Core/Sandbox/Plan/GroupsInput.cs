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
public sealed record GroupsInput(SandboxSettings? Settings, SandboxRepoSettings? Repo, AgentId Target, bool WriteScope, bool Attended, GithubCredentials Github, IReadOnlyList<string>? Omit);
