using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The Connections page's two questions about a pasted key, asked of the profile's Linear (its own url, else Linear's API): which teams it reads, and what it may write. A personal API key carries no readable scope,
/// so the second answer is Unknown once Linear accepts the key at all; the first save with it tells. The key goes only into the reader's Authorization header.
/// </summary>
/// <param name="live">The live profile, for the Linear url.</param>
/// <param name="engine">Checks the context settings.</param>
/// <param name="readers">Makes a team reader for the endpoint and the key.</param>
internal sealed class LinearKeyChecks(IProfileSource live, IContextEngine engine, Func<Uri, string, ILinearTeamReader> readers) : ILinearKeyChecks
{
    public async ValueTask<Result<IReadOnlyList<LinearConnectionTeamView>>> Teams(string key, CancellationToken cancellationToken)
    {
        var read = await Reader(key, cancellationToken).ConfigureAwait(false);
        var teams = await read.Details(cancellationToken).ConfigureAwait(false);
        return teams.IsError ? teams.Error : teams.Value.Select(static t => new LinearConnectionTeamView(t.Id, t.Key, t.Name)).ToList();
    }

    public async ValueTask<Result<LinearKeyScope>> WriteScope(string key, CancellationToken cancellationToken)
    {
        var read = await Reader(key, cancellationToken).ConfigureAwait(false);
        var teams = await read.Teams(cancellationToken).ConfigureAwait(false);
        return teams.IsError ? teams.Error : LinearKeyScope.Unknown;
    }

    private async ValueTask<ILinearTeamReader> Reader(string key, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var url = LinearConnect.DefaultUrl;
        if (loaded.IsSuccess)
        {
            ProfileRepoEntry[] entries = [.. (loaded.Value.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
            var settings = engine.CheckSettings(loaded.Value.Profile.Context, entries);
            url = settings is { IsSuccess: true, Value.Linear: { } linear } ? linear.Url : url;
        }

        return readers(url, key);
    }
}
