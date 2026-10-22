using System.Collections.Concurrent;

namespace Aw.Sindri.Core.Setup;

/// <summary>The repos known to have a 'repo.added' in the ledger, so the backfill does not look again.</summary>
public sealed class KnownRepoEvents
{
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="name"/> is known to have its event.</summary>
    /// <param name="name">The repo's name.</param>
    /// <returns>True when it is.</returns>
    public bool Has(string name) => _names.ContainsKey(name);

    /// <summary>Remembers that <paramref name="name"/> has its event.</summary>
    /// <param name="name">The repo's name.</param>
    public void Add(string name) => _names[name] = 0;
}
