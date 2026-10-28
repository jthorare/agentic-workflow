using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The measuring half of the build: the same reading and cuts without a write, and the cut of the current map.</summary>
internal sealed partial class BuildMapService
{
    /// <summary>
    /// What a build would save, measured and not saved ('context map checks --build', map structure spec §4.3): the same reading, the same cuts and the same join as <see cref="Handle"/>,
    /// then the structure measures. No model is called (the labeler and the concept namer are skipped) and nothing is written; the caller holds the heavy-job slot.
    /// </summary>
    /// <param name="request">The build's progress and scope.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The measures of the graph the build would save.</returns>
    internal async ValueTask<Result<MapChecksReport>> Measure(BuildMapRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        var mapped = Mapped(scope);
        if (mapped.IsError)
        {
            return mapped.Error;
        }

        var (settings, mappedRepos) = mapped.Value;
        // why: no graphify and no labeling here, however the profile is set: the same reading with a cluster source that has none
        var reading = new BuildMapService(inputs, new NoClusters(), namers, gate, defaults, store, clock);
        var read = await reading.ReadRepos(request.Progress, settings with { Labeling = new LabelingSettings { Enabled = false } }, mappedRepos, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var cut = await CutBasis(read.Value, scope.Repos, mappedRepos, cancellationToken).ConfigureAwait(false);
        return cut.IsError ? cut.Error : MapChecks.Measure(Join(WithoutDerivedConcepts(cut.Value.Kept), read.Value.Found), settings.CheckLimits);
    }

    private static Result<(ContextSettings Settings, IReadOnlyList<ContextRepo> Repos)> Mapped(ContextScope scope)
    {
        var mapped = SettingsReader.Read(scope.Settings).Bind(settings => ScopeRules.Resolve(settings, scope.Repos, scope.Caller).Map(r => (Settings: settings, Repos: r)));
        if (mapped.IsError)
        {
            return mapped.Error;
        }

        return mapped.Value.Repos.Count == 0
            ? ContextErrors.SettingsInvalid.With("context.repos: the profile maps no repo to build from (fix: add a repo to the profile and approve it)")
            : mapped.Value;
    }

    // The current map and the open draft as the build finds them, and the map the cuts keep of the current one (the deny globs, the demotions, the engine accepts the new rules lost) with the undo rows.
    private async ValueTask<Result<Cut>> CutBasis(Reading read, IReadOnlyList<ContextRepo> profileRepos, IReadOnlyList<ContextRepo> mappedRepos, CancellationToken cancellationToken)
    {
        var current = await store.Snapshot(draft: false, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var basis = current.Value?.Graph ?? GraphSnapshot.Empty;
        var open = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var at = clock.GetUtcNow();
        var ruleset = MapRuleset.Of(ScopeRules.Mapped(read.Settings, profileRepos));
        var changed = current.Value is { } map && !string.Equals(map.Version.Ruleset, ruleset, StringComparison.Ordinal);
        var cuts = new BuildCuts(profileRepos, mappedRepos, read.Found, changed, at);
        var (kept, undo, structure) = cuts.Apply(basis, current.Value?.Overrides ?? []);
        return new Cut(kept, UndoAlso(undo, open.Value, cuts), ruleset, at, structure);
    }

    // The cluster source of a measurement: graphify is not run.
    private sealed class NoClusters : IClusterSource
    {
        public ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(new ClusterSet([], Available: false, Reason: "not run")));
    }

    private sealed record Cut(GraphSnapshot Kept, IReadOnlyList<MapOverride> Undo, string Ruleset, DateTimeOffset At, StructureCounts Structure);
}
