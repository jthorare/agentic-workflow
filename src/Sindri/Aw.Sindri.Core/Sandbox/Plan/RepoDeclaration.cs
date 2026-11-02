using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The per-repo declaration (sandbox spec §3.7): 'sandbox.repos.&lt;repo&gt;'.</summary>
public static class RepoDeclaration
{
    /// <summary>The repo's declaration, or an empty one.</summary>
    /// <param name="settings">The profile's sandbox section, or null.</param>
    /// <param name="repo">The repo name.</param>
    /// <returns>The declaration.</returns>
    public static SandboxRepoSettings For(SandboxSettings? settings, RepoName repo) =>
        settings?.Repos is { } repos && repos.TryGetValue(repo.Value, out var declared) ? declared : new SandboxRepoSettings();
}
