using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.MapHeads;

/// <summary>
/// Where each mapped repo's checkout stands (F1): the repos a build would read (the same settings and scope rules), each at its HEAD and
/// whether the default branch holds that commit. Local refs only. A profile that maps no repo answers an empty list: the watcher has nothing to do.
/// </summary>
internal sealed class MapHeadsService(IRepoReader repos, IDefaultBranch defaults) : IService<MapHeadsRequest, IReadOnlyList<RepoHead>>
{
    public async ValueTask<Result<IReadOnlyList<RepoHead>>> Handle(MapHeadsRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        if (scope.Caller.Kind == ContextCallerKind.Session)
        {
            return ContextErrors.SessionsDisabled.With("reading the repo heads is a human or service job: a session can't");
        }

        var mapped = SettingsReader.Read(scope.Settings).Bind(settings => ScopeRules.Resolve(settings, scope.Repos, scope.Caller));
        if (mapped.IsError)
        {
            return mapped.Error;
        }

        var heads = new List<RepoHead>(mapped.Value.Count);
        foreach (var repo in mapped.Value)
        {
            var head = await repos.Head(repo, cancellationToken).ConfigureAwait(false);
            if (head.IsError)
            {
                return head.Error;
            }

            var onDefault = await defaults.Reaches(repo, head.Value, cancellationToken).ConfigureAwait(false);
            if (onDefault.IsError)
            {
                return onDefault.Error;
            }

            heads.Add(new RepoHead(repo.Name, head.Value, onDefault.Value));
        }

        return heads;
    }
}
