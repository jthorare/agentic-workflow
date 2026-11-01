using Aw.Context.Core.Engine;
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
        // the publish status is a service's read (a session is refused it), and only derived words leave here (the map's version, stale, the phase, the change of a pin)
        // Repos is deliberately the whole profile's (ContextScopes builds it from every profile repo for any caller), so Running(scope) is the whole-map ruleset and stale matches the refresh tick
        var service = scope with { Caller = new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service) };
        var state = await engine.Held(service, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError || latest.IsError || state.IsError)
        {
            return loaded.IsError ? loaded.Error : latest.IsError ? latest.Error : state.Error;
        }

        var changes = new PinChanges(MapRuleset.RunningPins(scope), state.Value.CurrentPinHashes);
        var pins = Rows(loaded.Value.Profile, latest.Value?.Profile, repo, changes, state.Value.CurrentHeads);
        var progress = await rebuild.Read(service, cancellationToken).ConfigureAwait(false);
        var failed = await LastFailure(cancellationToken).ConfigureAwait(false);
        // a refresh covers every repo and its retry line carries the error text of a failed build, which can name another repo: a session reads the phase, never the line
        var view = new ContextRebuildView(Phase(progress.Phase), scope.Caller.Kind == ContextCallerKind.Session ? null : progress.Progress, progress.RetryAt, Stale(state.Value, scope), failed);
        return new ContextPinsView(1, repo, loaded.Value.Hash.Value, state.Value.Current is { } current ? MapVersionRef.Tag(current) : null, pins, view);
    }

    // Whether the answer covers every repo the map was built from: no repo addressed, or the only repo both of the profile and of the current map's heads
    // (a repo the profile dropped stays in the map until the rebuild lands, and its pins' ids must not reach an answer for another repo). A map with no heads (an imported one)
    // can't say which repos it covers, so a one-repo answer never lists its removed pins.
    private static bool EveryRepo(SindriProfile profile, string? repo, IReadOnlyList<MapHead> heads) =>
        repo is null
        || (heads.Count > 0
            && (profile.Repos ?? []).All(r => string.Equals(r.Name, repo, StringComparison.Ordinal))
            && heads.All(h => string.Equals(h.Repo.Value, repo, StringComparison.Ordinal)));

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

    private static List<ContextPinRow> Rows(SindriProfile liveProfile, SindriProfile? approvedProfile, string? repo, PinChanges changes, IReadOnlyList<MapHead> heads)
    {
        var diff = ProfileDiff.Changes(approvedProfile, liveProfile).ToDictionary(static c => c.Key, StringComparer.Ordinal);
        var live = Identified(liveProfile);
        var approved = approvedProfile is null ? [] : Identified(approvedProfile);
        var liveKeys = live.Select(static p => p.Pin.Key).ToHashSet(StringComparer.Ordinal);
        var rows = new List<ContextPinRow>();
        foreach (var (pin, id) in live.Where(p => InScope(p.Pin, repo)))
        {
            rows.Add(Row(pin, id, diff.ContainsKey(pin.Key) ? PinStates.Waiting : PinStates.Approved, changes.Of(approved.FirstOrDefault(a => a.Pin.Key == pin.Key).Id)));
        }

        rows.AddRange(approved.Where(p => InScope(p.Pin, repo) && !liveKeys.Contains(p.Pin.Key) && diff.TryGetValue(p.Pin.Key, out var change) && change.After is null)
            .Select(p => Row(p.Pin, p.Id, PinStates.Removing, changes.Of(p.Id))));

        // pins the map still has that neither profile does any more: only their ids are known, and the map's hashes carry no repo, so only an answer for every repo may list them
        // (one with no repo, or the only repo of the profile and of the current map). Their state is 'approved' because the removal itself is approved: it is the map that hasn't caught up.
        rows.AddRange(!EveryRepo(approvedProfile ?? liveProfile, repo, heads) ? [] : changes.RemovedIds().Select(static id => new ContextPinRow(id, id, Summary: null, [], [], FolderRules: 0, "profile", PinStates.Approved, PinChanges.Removed)));
        return rows;
    }

    // Each entry with the id the map gives it (null id for an entry the approved profile lacks is never asked for).
    private static List<(ProfilePin Pin, string Id)> Identified(SindriProfile profile)
    {
        var pins = ProfilePins.Of(profile);
        var ids = ProfilePins.Ids(profile, pins);
        return [.. pins.Select((p, i) => (p, ids[i]))];
    }

    // The profile's own pins belong to every repo; a repo's own entry only to it. No repo named (a human on a profile with several) is every pin.
    private static bool InScope(ProfilePin pin, string? repo) =>
        repo is null || pin.Repo is null || string.Equals(pin.Repo, repo, StringComparison.Ordinal);

    private static ContextPinRow Row(ProfilePin pin, string id, string state, string? change)
    {
        var settings = pin.Settings;
        return new ContextPinRow(id, settings.Name ?? string.Empty, settings.Summary, Listed(settings.Aliases), Listed(settings.Features),
            Listed(settings.Paths).Count + Listed(settings.DependsOn).Count + Listed(settings.Owns).Count, pin.SetIn, state, change);
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
