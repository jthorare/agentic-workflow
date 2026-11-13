using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host;

/// <summary>What 'wf validate' knows about names outside the document: the repos of the profile, the connections and the memories, each when its source can be read.</summary>
internal sealed class ProfileGraphWorld : IGraphWorld
{
    private readonly HashSet<string>? _repos;
    private readonly HashSet<string>? _connections;
    private readonly HashSet<string>? _memories;

    private ProfileGraphWorld(HashSet<string>? repos, HashSet<string>? connections, HashSet<string>? memories)
    {
        _repos = repos;
        _connections = connections;
        _memories = memories;
    }

    /// <summary>Reads the profile's repo names; with no profile to read, every repo is unavailable.</summary>
    /// <param name="profile">The profile port, or null when the container has none.</param>
    /// <param name="connections">The connection store, or null when there is no ledger: connection names are then unavailable.</param>
    /// <param name="memories">The memory store, or null when there is no ledger: memory names are then unavailable.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The world.</returns>
    public static async Task<ProfileGraphWorld> Load(IApprovedProfile? profile, IConnectionStore? connections, IMemoryStore? memories, CancellationToken ct)
    {
        HashSet<string>? repos = null;
        if (profile is not null)
        {
            var latest = await profile.Latest(ct).ConfigureAwait(false);
            if (latest.IsSuccess && latest.Value is { } loaded)
            {
                repos = [.. (loaded.Profile.Repos ?? []).Select(static r => r.Name)];
            }
        }

        HashSet<string>? names = null;
        if (connections is not null && await connections.List(ct).ConfigureAwait(false) is { IsSuccess: true } connectionRows)
        {
            names = [.. connectionRows.Value.Select(static c => c.Name.ToString())];
        }

        HashSet<string>? memoryNames = null;
        if (memories is not null && await memories.List(ct).ConfigureAwait(false) is { IsSuccess: true } memoryRows)
        {
            memoryNames = [.. memoryRows.Value.Select(static m => m.Name.Value)];
        }

        return new ProfileGraphWorld(repos, names, memoryNames);
    }

    public GraphLookup Repo(string name) => _repos is null ? GraphLookup.Unavailable : _repos.Contains(name) ? GraphLookup.Known : GraphLookup.Unknown;

    public GraphLookup Connection(string name) => _connections is null ? GraphLookup.Unavailable : _connections.Contains(name) ? GraphLookup.Known : GraphLookup.Unknown;

    public GraphLookup Memory(string name) => _memories is null ? GraphLookup.Unavailable : _memories.Contains(name) ? GraphLookup.Known : GraphLookup.Unknown;
}
