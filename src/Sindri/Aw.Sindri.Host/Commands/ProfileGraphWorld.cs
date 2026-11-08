using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>What 'wf validate' knows about names outside the document: the repos of the profile, when one can be read. Connections and memory stores don't exist yet, so those are unavailable.</summary>
internal sealed class ProfileGraphWorld : IGraphWorld
{
    private readonly HashSet<string>? _repos;

    private ProfileGraphWorld(HashSet<string>? repos)
    {
        _repos = repos;
    }

    /// <summary>Reads the profile's repo names; with no profile to read, every repo is unavailable.</summary>
    /// <param name="profile">The profile port, or null when the container has none.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The world.</returns>
    public static async Task<ProfileGraphWorld> Load(IApprovedProfile? profile, CancellationToken ct)
    {
        if (profile is null)
        {
            return new ProfileGraphWorld(repos: null);
        }

        var latest = await profile.Latest(ct).ConfigureAwait(false);
        return new ProfileGraphWorld(latest.IsSuccess && latest.Value is { } loaded
            ? [.. (loaded.Profile.Repos ?? []).Select(static r => r.Name)]
            : null);
    }

    public GraphLookup Repo(string name) => _repos is null ? GraphLookup.Unavailable : _repos.Contains(name) ? GraphLookup.Known : GraphLookup.Unknown;

    public GraphLookup Connection(string name) => GraphLookup.Unavailable;

    public GraphLookup Memory(string name) => GraphLookup.Unavailable;
}
