using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.BuildMap;

internal sealed partial class BuildMapService
{
    // The newest draft when it finished after this build asked for the gate, under the same rules, at exactly the commits the mapped repos stand at now; null on any mismatch or read error (the build then runs as usual).
    private async ValueTask<MapBuildReport?> SavedWhileWaiting(ContextSettings settings, IReadOnlyList<ContextRepo> mappedRepos, IReadOnlyList<ContextRepo> profileRepos, DateTimeOffset asked, CancellationToken cancellationToken)
    {
        var open = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (open.IsError || open.Value is not { } draft || draft.Version.Created < asked
            || !string.Equals(draft.Version.Ruleset, MapRuleset.Of(ScopeRules.Mapped(settings, profileRepos)), StringComparison.Ordinal))
        {
            return null;
        }

        var saved = await store.Heads(draft.Version.Number, cancellationToken).ConfigureAwait(false);
        if (saved.IsError || !await StandAt(mappedRepos, saved.Value, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var number = draft.Version.Number.ToString(CultureInfo.InvariantCulture);
        return new MapBuildReport(draft.Version, [], [], saved.Value) { Notes = [$"v{number} was built from these heads while this build waited, so it was not built again"] };
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
