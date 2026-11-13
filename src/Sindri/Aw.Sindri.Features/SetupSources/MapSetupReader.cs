using Aw.CodeIndex.Core.Facade;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the context map: whether the engine's stored state holds a current map (through the facade, as a service sees the whole map), and while it doesn't,
/// the build jobs: a map build or a code index build of a mapped repo (the first map builds after it, #3112) running is "building" with its stage and its n/N when its
/// lines say so; a failed latest one is "failed". With no approved profile or no repo there is nothing to build from.
/// </summary>
/// <param name="approvals">The approved profile (its context section and repos make the scope).</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The 'jobs' view.</param>
internal sealed class MapSetupReader(IApprovedProfile approvals, IContextEngine engine, IJobViews jobs)
{
    private static readonly JobKind _build = JobKind.From("BuildContextMap");

    // The code index's build job (its slice owns the kind; AW1003 keeps slices off each other's types).
    private static readonly JobKind _index = JobKind.From("BuildCodeIndex");

    internal async ValueTask<Result<MapFact>> Read(CancellationToken cancellationToken)
    {
        var fact = await Phase(cancellationToken).ConfigureAwait(false);
        if (fact.IsError)
        {
            return fact;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var state = fact.Value.Phase switch
        {
            MapPhase.Built => SetupState.Done,
            MapPhase.Building => SetupState.Running,
            MapPhase.Failed => SetupState.Failed,
            MapPhase.Waiting or MapPhase.NotBuilt => SetupState.Todo,
            _ => throw new InvalidOperationException("Not a map phase"),
        };
        var repos = approved.IsSuccess ? approved.Value?.Profile.Repos ?? [] : [];
        return fact.Value with { Items = new SetupItems([.. repos.Select(r => new SetupItem(r.Name, state, Detail: null))]) };
    }

    private async ValueTask<Result<MapFact>> Phase(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (approved.Value is not { } loaded || (loaded.Profile.Repos?.Count ?? 0) == 0)
        {
            return new MapFact(MapPhase.Waiting);
        }

        var status = await engine.MapStatus(anchor: null, Scope(loaded.Profile), cancellationToken).ConfigureAwait(false);
        if (status.IsSuccess)
        {
            return new MapFact(MapPhase.Built);
        }

        return status.Error.Code == ContextErrors.NoMap.Code ? await Builds(loaded.Profile, cancellationToken).ConfigureAwait(false) : status.Error;
    }

    // The approved profile's scope as a service reads it (the deny globs apply), as doctor builds it.
    private static ContextScope Scope(SindriProfile profile) =>
        new(profile.Context, ScopeRules.Usable([.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path, RepoDenyGlobs.For(profile, r), [.. RepoConcepts.For(profile, r).Select(c => ConceptPin.Of(c.Id is { } id ? ConceptId.From(id.Value) : null, c.Name, c.Summary, c.Features, c.Paths).Declaring(c.DependsOn, c.Owns).WithAliases(c.Aliases))], DemotePaths: RepoDemoteGlobs.For(profile, r)) { Roles = RepoRoleSets.For(profile, r) is { } sets ? [.. sets.Select(x => new RepoRole(x.Name, x.Demoted, x.Globs))] : null })]), new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));

    // No map yet: a running map build is under way, else a running code index build of a mapped repo (#3112); else the newest ended build says whether the last try failed.
    private async ValueTask<Result<MapFact>> Builds(SindriProfile profile, CancellationToken cancellationToken)
    {
        var maps = await jobs.List(state: null, _build, cancellationToken).ConfigureAwait(false);
        if (maps.IsError)
        {
            return maps.Error;
        }

        var indexes = await jobs.List(state: null, _index, cancellationToken).ConfigureAwait(false);
        if (indexes.IsError)
        {
            return indexes.Error;
        }

        if (maps.Value.FirstOrDefault(static j => j.State == JobState.Running) is { } running)
        {
            // The newest files line (#3433), the one the whole map's 'building' answer reads too; a repo with no files (0/0) gives no count.
            var files = MapBuildFiles.Newest(running.Progress.OrderBy(static l => l.Round).Select(static l => l.Line));
            return new MapFact(MapPhase.Building, files is { Total: > 0 } ? new SetupProgress(files.Done, files.Total, "files") : null) { Stage = MapStage.Map };
        }

        var mapped = (profile.Repos ?? []).Select(static r => r.Name).ToList();
        var ours = indexes.Value.Where(j => mapped.Exists(r => IndexBuildWords.IsBuildOf(j, r))).ToList();
        if (ours.FirstOrDefault(static j => j.State == JobState.Running) is { } indexing)
        {
            var files = IndexBuildFiles.Newest(indexing.Progress.OrderBy(static l => l.Round).Select(static l => l.Line));
            return new MapFact(MapPhase.Building, files is { Total: > 0 } ? new SetupProgress(files.Done, files.Total, "files") : null) { Stage = MapStage.Index };
        }

        // A failed index build counts only when no map build ended after it: the index rebuilds on every commit, and its success must not hide a failed map.
        var map = Ended(maps.Value);
        var index = Ended(ours);
        var failed = index is { State: JobState.Failed } && (map is null || index.Sequence > map.Sequence) ? index : map is { State: JobState.Failed } ? map : null;
        return failed is null ? new MapFact(MapPhase.NotBuilt) : new MapFact(MapPhase.Failed, FailureCode: failed.Code);
    }

    // The newest build that was not cancelled (the running ones were read before), as the map step read it before #3112.
    private static JobRow? Ended(IEnumerable<JobRow> builds) =>
        builds.Where(static j => j.State != JobState.Cancelled).MaxBy(static j => j.Sequence);
}
