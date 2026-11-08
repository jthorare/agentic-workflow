using System.Collections.Concurrent;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>What the automatic MCP registration has already looked at in this serve process (setup-once spec §5.4, SO-A4): per repo, the approved profile hash and the port. A new process starts empty, so its first tick looks at every repo.</summary>
internal sealed class McpAutoMemory
{
    private readonly ConcurrentDictionary<RepoName, (ProfileHash Profile, int Port)> _seen = new();

    /// <summary>Whether <paramref name="repo"/> was already looked at under this profile and port.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="profile">The approved profile's hash.</param>
    /// <param name="port">The control plane's port.</param>
    /// <returns>True when nothing changed since.</returns>
    internal bool Seen(RepoName repo, ProfileHash profile, int port) => _seen.TryGetValue(repo, out var last) && last == (profile, port);

    /// <summary>Records that <paramref name="repo"/> was looked at under this profile and port.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="profile">The approved profile's hash.</param>
    /// <param name="port">The control plane's port.</param>
    internal void Remember(RepoName repo, ProfileHash profile, int port) => _seen[repo] = (profile, port);
}
