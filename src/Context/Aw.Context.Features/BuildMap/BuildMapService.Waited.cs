using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.BuildMap;

internal sealed partial class BuildMapService
{
    // The newest draft's version row, or null when there is none or the store can't say (a draft's date is stamped before its concepts are named, so only its number tells whether it was saved while a build waited).
    private async ValueTask<MapVersion?> NewestDraft(CancellationToken cancellationToken)
    {
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        return versions.IsError ? null : versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Draft);
    }

    // The newest draft when it is not the one seen before the wait (so it was saved during it), under the same rules, at exactly the commits the mapped repos stand at now; null on any mismatch or read error (the build then runs as usual).
    private async ValueTask<MapBuildReport?> SavedWhileWaiting(ContextSettings settings, IReadOnlyList<ContextRepo> mappedRepos, IReadOnlyList<ContextRepo> profileRepos, MapVersion? before, CancellationToken cancellationToken)
    {
        if (await NewestDraft(cancellationToken).ConfigureAwait(false) is not { } draft || draft.Number == before?.Number
            || !string.Equals(draft.Ruleset, MapRuleset.Of(ScopeRules.Mapped(settings, profileRepos)), StringComparison.Ordinal))
        {
            return null;
        }

        var saved = await store.Heads(draft.Number, cancellationToken).ConfigureAwait(false);
        if (saved.IsError || !await StandAt(mappedRepos, saved.Value, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var number = draft.Number.ToString(CultureInfo.InvariantCulture);
        return new MapBuildReport(draft, [], [], saved.Value) { Notes = [$"v{number} was built from these heads while this build waited, so it was not built again"] };
    }

    // Whether the mapped repos are exactly the repos of the saved heads, each at its saved commit.
    private async ValueTask<bool> StandAt(IReadOnlyList<ContextRepo> mappedRepos, IReadOnlyList<MapHead> saved, CancellationToken cancellationToken)
    {
        if (saved.Count != mappedRepos.Count)
        {
            return false;
        }

        foreach (var repo in mappedRepos)
        {
            var head = await inputs.Repos.Head(repo, cancellationToken).ConfigureAwait(false);
            if (head.IsError || !saved.Any(h => h.Repo == repo.Name && string.Equals(h.Commit, head.Value, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        return true;
    }
}
