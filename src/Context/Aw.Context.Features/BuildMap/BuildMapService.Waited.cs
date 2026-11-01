using System.Globalization;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Features.BuildMap;

internal sealed partial class BuildMapService
{
    // The newest version row that is a draft or the current map (a draft the build ahead saved may be promoted before this one looks), or null when there is none or the store can't say. A draft's date is stamped before its concepts are named, so only its number tells whether it was saved while a build waited.
    private async ValueTask<MapVersion?> NewestSaved(CancellationToken cancellationToken)
    {
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        return versions.IsError ? null : versions.Value.Where(v => v.Status is MapVersionStatus.Draft or MapVersionStatus.Current).MaxBy(v => v.Number);
    }

    // The newest draft or current version when it is not the one seen before the wait (so it was saved during it), under the same rules, at exactly the commits the mapped repos stand at now; null on any mismatch or read error (the build then runs as usual).
    private async ValueTask<MapBuildReport?> SavedWhileWaiting(ContextSettings settings, IReadOnlyList<ContextRepo> mappedRepos, IReadOnlyList<ContextRepo> profileRepos, MapVersion? before, CancellationToken cancellationToken)
    {
        if (await NewestSaved(cancellationToken).ConfigureAwait(false) is not { } draft || draft.Number <= (before?.Number ?? 0)
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
            if (head.IsError || saved.FirstOrDefault(h => h.Repo == repo.Name) is not { } held || !string.Equals(held.Commit, head.Value, StringComparison.Ordinal))
            {
                return false;
            }

            // #1704: a draft built before the code index of its commit has unresolved symbol citations; it is reused only when the index commit it read is the one the build would read now. Both absent counts as the same (a rebuild would read the same nothing); one absent and the other set does not.
            var index = await inputs.Records.Symbols.Heads(repo.Name, asOf: null, cancellationToken).ConfigureAwait(false);
            if (index.IsError || !SameIndexReading(held.IndexCommit, IndexCommitOf(index.Value, head.Value)))
            {
                return false;
            }
        }

        return true;
    }

    // The commit of the code index when it holds a readable index at exactly the commit given (either may be abbreviated); null when it is missing, building, damaged or at another commit.
    private static string? IndexCommitOf(CodeIndexHead index, string commit) =>
        index.State is CodeIndexState.Missing or CodeIndexState.Damaged or CodeIndexState.Building or CodeIndexState.NotConfigured
        || index.Commit is not { } indexed || !CodeIndexReading.Same(indexed, commit)
            ? null
            : indexed;

    // Both absent is the same reading (a rebuild would read the same nothing); one absent is not; both set compare as commits.
    private static bool SameIndexReading(string? used, string? current) =>
        used is null || current is null ? used is null && current is null : CodeIndexReading.Same(used, current);
}
