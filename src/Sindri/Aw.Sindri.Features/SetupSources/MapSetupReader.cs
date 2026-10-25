using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the context map: whether the engine's stored state holds a current map (through the facade, as a service sees the whole map), and while it doesn't,
/// the build jobs: one running is "building" with its n/N when its lines say so, a failed latest one is "failed". With no approved profile or no repo there is nothing to build from.
/// </summary>
/// <param name="approvals">The approved profile (its context section and repos make the scope).</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The 'jobs' view.</param>
internal sealed class MapSetupReader(IApprovedProfile approvals, IContextEngine engine, IJobViews jobs)
{
    private static readonly JobKind _build = JobKind.From("BuildContextMap");

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

        return status.Error.Code == ContextErrors.NoMap.Code ? await Builds(cancellationToken).ConfigureAwait(false) : status.Error;
    }

    // The approved profile's scope as a service reads it (the deny globs apply), as doctor builds it.
    private static ContextScope Scope(SindriProfile profile) =>
        new(profile.Context, ScopeRules.Usable([.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path, RepoDenyGlobs.For(profile, r), [.. RepoConcepts.For(profile, r).Select(c => ConceptPin.Of(c.Id is { } id ? ConceptId.From(id.Value) : null, c.Name, c.Summary, c.Features, c.Paths))]))]), new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));

    // No map yet: a running build is under way; else the newest ended one says whether the last try failed.
    private async ValueTask<Result<MapFact>> Builds(CancellationToken cancellationToken)
    {
        var recent = await jobs.List(state: null, _build, cancellationToken).ConfigureAwait(false);
        if (recent.IsError)
        {
            return recent.Error;
        }

        if (recent.Value.FirstOrDefault(static j => j.State == JobState.Running) is { } running)
        {
            var last = running.Progress.MaxBy(static l => l.Round);
            return new MapFact(MapPhase.Building, last is null ? null : MapBuildProgress.Parse(last.Line));
        }

        return recent.Value.FirstOrDefault(static j => j.State != JobState.Cancelled) is { State: JobState.Failed } failed
            ? new MapFact(MapPhase.Failed, FailureCode: failed.Code)
            : new MapFact(MapPhase.NotBuilt);
    }
}
