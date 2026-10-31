using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The pins read (concept pins spec §3.6 to §3.8). The pins and the file hash come from one load of the live profile file, so they can't disagree. A pin's state comes only from the approved-vs-live
/// diff of the profile (<see cref="ProfileDiff"/>, keyed by the entry's id): no diff row is 'approved', a new or changed entry is 'waiting', an approved entry the live file dropped is 'removing' and listed after the live ones.
/// The repo scope is the engine's (<see cref="RepoAddressing"/>): a session sees the profile's pins and its own repo's only. A pin's <c xml:lang="en">change</c> compares the approved entry's hash with the one the
/// current map stored for that pin (<see cref="PinChanges"/>); a map that stored none leaves it null.
/// </summary>
internal sealed class GetContextPinsService(ContextScopes scopes, IContextEngine engine, IProfileSource live, IApprovedProfile approved, MapRebuildState rebuild, IJobViews jobs) : IService<GetContextPinsRequest, ContextPinsView>
{
    public async ValueTask<Result<ContextPinsView>> Handle(GetContextPinsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var repo = Addressed(scope.Value, request.Repo);
        return repo.IsError ? repo.Error : await Read(scope.Value, repo.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ContextPinsView>> Read(ContextScope scope, string? repo, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var state = await engine.Held(scope, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError || latest.IsError || state.IsError)
        {
            return loaded.IsError ? loaded.Error : latest.IsError ? latest.Error : state.Error;
        }

        var changes = new PinChanges(MapRuleset.RunningPins(scope), state.Value.CurrentPinHashes);
        var pins = Rows(loaded.Value.Profile, latest.Value?.Profile, repo, changes);
        var progress = await rebuild.Read(cancellationToken).ConfigureAwait(false);
        var failed = await LastFailure(cancellationToken).ConfigureAwait(false);
        var view = new ContextRebuildView(Phase(progress.Phase), progress.Progress, progress.RetryAt, Stale(state.Value, scope), failed);
        return new ContextPinsView(1, repo, loaded.Value.Hash.Value, state.Value.Current is { } current ? MapVersionRef.Tag(current) : null, pins, view);
    }

    // The repo the answer is for, by the rule every map read shares; null is every pin.
    private static Result<string?> Addressed(ContextScope scope, string? asked)
    {
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var addressed = RepoAddressing.Addressed(settings.Value, scope, asked);
        return addressed.IsError ? addressed.Error : addressed.Value.Name;
    }

    private static List<ContextPinRow> Rows(SindriProfile liveProfile, SindriProfile? approvedProfile, string? repo, PinChanges changes)
    {
        var diff = ProfileDiff.Changes(approvedProfile, liveProfile).ToDictionary(static c => c.Key, StringComparer.Ordinal);
        var liveKeys = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<ContextPinRow>();
        foreach (var pin in ProfilePins.Of(liveProfile).Where(p => InScope(p, repo)))
        {
            _ = liveKeys.Add(pin.Key);
            rows.Add(Row(pin, diff.ContainsKey(pin.Key) ? PinStates.Waiting : PinStates.Approved, changes));
        }

        if (approvedProfile is not null)
        {
            rows.AddRange(ProfilePins.Of(approvedProfile).Where(p => InScope(p, repo) && !liveKeys.Contains(p.Key) && diff.TryGetValue(p.Key, out var change) && change.After is null).Select(p => Row(p, PinStates.Removing, changes)));
        }

        // pins the map still has that neither profile does any more: only their ids are known
        rows.AddRange(changes.RemovedIds().Select(static id => new ContextPinRow(id, id, Summary: null, [], [], FolderRules: 0, "profile", PinStates.Approved, PinChanges.Removed)));
        return rows;
    }

    // The profile's own pins belong to every repo; a repo's own entry only to it. No repo named (a human on a profile with several) is every pin.
    private static bool InScope(ProfilePin pin, string? repo) =>
        repo is null || pin.Repo is null || string.Equals(pin.Repo, repo, StringComparison.Ordinal);

    private static ContextPinRow Row(ProfilePin pin, string state, PinChanges changes)
    {
        var settings = pin.Settings;
        var name = settings.Name ?? string.Empty;
        var id = settings.Id?.Value ?? PinSlug.Of(name);
        return new ContextPinRow(id, name, settings.Summary, Listed(settings.Aliases), Listed(settings.Features),
            Listed(settings.Paths).Count + Listed(settings.DependsOn).Count + Listed(settings.Owns).Count, pin.SetIn, state, changes.Of(id));
    }

    private static IReadOnlyList<string> Listed(IReadOnlyList<string>? list) => list ?? [];

    // Stale as the refresh tick decides it (ContextMapRefreshService.RulesChanged): a map built under other rules than the approved profile's, unless a held draft is already on its way to replace it.
    private static bool Stale(MapPublishState state, ContextScope scope) =>
        state.Current is not null && state.Hold is null && MapRuleset.Running(scope) is { } running && !string.Equals(state.CurrentRuleset, running, StringComparison.Ordinal);

    // The newest ended refresh, when it failed: a later success clears it.
    private async ValueTask<string?> LastFailure(CancellationToken cancellationToken)
    {
        var refreshes = await jobs.List(state: null, ContextMapRefreshService.RefreshKind, cancellationToken).ConfigureAwait(false);
        var newest = refreshes.IsError ? null : refreshes.Value.Where(static j => j.EndedAt is not null).MaxBy(static j => j.EndedAt);
        return newest is { State: JobState.Failed } ? newest.Code : null;
    }

    private static string Phase(MapRebuildPhase phase) => phase switch
    {
        MapRebuildPhase.Building => PinPhases.Building,
        MapRebuildPhase.MapHeld => PinPhases.MapHeld,
        MapRebuildPhase.NoMainBranch => PinPhases.NoMainBranch,
        MapRebuildPhase.Backoff => PinPhases.Backoff,
        MapRebuildPhase.Idle => PinPhases.Idle,
        _ => PinPhases.Idle,
    };
}
