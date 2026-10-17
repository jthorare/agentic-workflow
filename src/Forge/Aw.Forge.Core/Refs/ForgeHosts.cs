namespace Aw.Forge.Core.Refs;

/// <summary>The hosts the profile allows (workflow.forge.hosts). Compared case-insensitively; an empty set allows nothing.</summary>
public sealed class ForgeHosts
{
    private readonly HashSet<string> _hosts;

    /// <summary>Creates the set.</summary>
    /// <param name="hosts">Host names such as github.com.</param>
    public ForgeHosts(IEnumerable<string> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        _hosts = new HashSet<string>(
            hosts.Select(h => h.Trim().ToLowerInvariant()).Where(h => h.Length > 0),
            StringComparer.Ordinal);
    }

    /// <summary>The allowed hosts, lower case.</summary>
    public IReadOnlyCollection<string> Names => _hosts;

    /// <summary>Whether a host is allowed.</summary>
    /// <param name="host">The host name.</param>
    /// <returns>True when listed.</returns>
    public bool Allows(string host) => _hosts.Contains(host.Trim().ToLowerInvariant());
}
