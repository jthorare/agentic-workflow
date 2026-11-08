using System.Collections.Concurrent;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// The profile repo of a session's directory (collector spec §4.1 step 7, §4.3): resolved once per directory, off the pass, and cached. A row gets its repo on the pass
/// after the lookup finished. An entry is dropped when the approved profile changes and after five minutes (the directory may be gone).
/// </summary>
internal sealed class SessionRepos(IRepoLocator locator, IApprovedProfile approved, TimeProvider clock)
{
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _profileEvery = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (RepoName? Repo, DateTimeOffset At)> _known = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _pending = new(StringComparer.Ordinal);
    private LoadedProfile? _profile;
    private DateTimeOffset _profileAt = DateTimeOffset.MinValue;

    /// <summary>The cached repo of <paramref name="directory"/>, null when unresolved or outside every repo; starts a lookup when none is cached or pending.</summary>
    internal RepoName? Lookup(string? directory)
    {
        if (directory is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (_known.TryGetValue(directory, out var hit) && now - hit.At < _ttl)
        {
            return hit.Repo;
        }

        if (_pending.TryAdd(directory, value: true))
        {
            _ = Resolve(directory);
        }

        return _known.TryGetValue(directory, out var stale) ? stale.Repo : null;
    }

    private async Task Resolve(string directory)
    {
        try
        {
            var profile = await Profile().ConfigureAwait(false);
            var repo = profile is null ? null : await locator.Locate(directory, profile.Profile.Repos ?? [], CancellationToken.None).ConfigureAwait(false);
            _known[directory] = (repo, clock.GetUtcNow());
        }
        finally
        {
            _ = _pending.TryRemove(directory, out _);
        }
    }

    private async Task<LoadedProfile?> Profile()
    {
        var now = clock.GetUtcNow();
        if (now - _profileAt < _profileEvery)
        {
            return _profile;
        }

        var latest = await approved.Latest(CancellationToken.None).ConfigureAwait(false);
        var loaded = latest.IsSuccess ? latest.Value : null;
        if (loaded?.Hash != _profile?.Hash)
        {
            _known.Clear();
        }

        _profile = loaded;
        _profileAt = now;
        return loaded;
    }
}
