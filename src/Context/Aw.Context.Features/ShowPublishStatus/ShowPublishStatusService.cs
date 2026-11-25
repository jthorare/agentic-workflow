using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowPublishStatus;

/// <summary>
/// The publish state for 'context map status', the Health card and a session (retrieval spec §8.7a): read-only, and cut to the caller's repos, so a session
/// sees its own repo's heads and no other repo's name, commit or count. Sessions are on the same terms as any session read (020 while off, 003 for a repo the profile doesn't map).
/// </summary>
internal sealed class ShowPublishStatusService(IGraphStore store) : IService<ShowPublishStatusRequest, MapPublishView>
{
    public async ValueTask<Result<MapPublishView>> Handle(ShowPublishStatusRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        if (MapAccess.Gate(scope) is { } refused)
        {
            return refused;
        }

        var repos = SettingsReader.Read(scope.Settings).Bind(settings => ScopeRules.Resolve(settings, scope.Repos, scope.Caller, scope.LeftOut));
        if (repos.IsError)
        {
            return repos.Error;
        }

        var state = await store.Held(cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        var names = repos.Value.Select(r => r.Name).ToList();
        IReadOnlyList<MapHead> Cut(IReadOnlyList<MapHead> heads) => [.. heads.Where(h => names.Contains(h.Repo))];
        var hold = state.Value.Hold is { } held ? held with { Heads = Cut(held.Heads) } : null;
        return new MapPublishView(names, hold, state.Value.LatestDraft, state.Value.Current, Cut(state.Value.CurrentHeads));
    }
}
