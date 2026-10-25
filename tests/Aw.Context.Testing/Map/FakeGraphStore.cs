using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>The in-memory graph store; <see cref="GraphStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeGraphStore : IGraphStore, IProposalRuns
{
    private readonly List<(MapVersion Version, GraphSnapshot Graph)> _versions = [];
    private readonly List<MapOverride> _overrides = [];
    private readonly Dictionary<string, ScopeProposal> _runs = new(StringComparer.Ordinal);
    private readonly Dictionary<int, IReadOnlyList<MapHead>> _heads = [];
    private int? _hold;
    private DateTimeOffset? _chain;
    private int _last;   // version numbers are never reused, as with the SQLite store's AUTOINCREMENT

    /// <summary>Called after every snapshot read, with whether it was of the draft: a test's way to make another caller write between a service's read and its write.</summary>
    public Action<bool>? AfterSnapshot { get; set; }

    /// <summary>When set, every call fails with it (a store that can't open).</summary>
    public Error? Failure { get; set; }

    /// <summary>When set, <see cref="ReplaceDraft"/> fails with it and changes nothing (a write that can't commit).</summary>
    public Error? ReplaceDraftFailure { get; set; }

    /// <summary>When set, <see cref="SaveDraft"/> fails with it and changes nothing.</summary>
    public Error? SaveDraftFailure { get; set; }

    /// <summary>When set, <see cref="Promote"/> fails with it and changes nothing.</summary>
    public Error? PromoteFailure { get; set; }

    /// <summary>Records the map rules the current version was built under (issue 1041: the fake's stand-in for a build that stamps its ruleset).</summary>
    /// <param name="ruleset">The hash.</param>
    public void StampCurrentRuleset(string ruleset)
    {
        var at = _versions.FindIndex(v => v.Version.Status == MapVersionStatus.Current);
        _versions[at] = _versions[at] with { Version = _versions[at].Version with { Ruleset = ruleset } };
    }

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion?>> Current(CancellationToken cancellationToken) =>
        Done(() => Results.Success(_versions.Select(v => v.Version).FirstOrDefault(v => v.Status == MapVersionStatus.Current)));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<MapVersion>>> Versions(CancellationToken cancellationToken) =>
        Done(() => Results.Success<IReadOnlyList<MapVersion>>([.. _versions.Select(v => v.Version).OrderByDescending(v => v.Number)]));

    /// <inheritdoc/>
    public ValueTask<Result<GraphSnapshot>> Read(int version, CancellationToken cancellationToken) =>
        Done(() => Results.Success(_versions.Find(v => v.Version.Number == version).Graph ?? GraphSnapshot.Empty));

    /// <inheritdoc/>
    public ValueTask<Result<MapSnapshot?>> Snapshot(bool draft, CancellationToken cancellationToken) => Done(() =>
    {
        var wanted = draft ? MapVersionStatus.Draft : MapVersionStatus.Current;
        var found = _versions.Where(v => v.Version.Status == wanted).OrderByDescending(v => v.Version.Number).Select(v => ((MapVersion, GraphSnapshot)?)v).FirstOrDefault();
        IReadOnlyList<MapOverride> overrides = [.. _overrides.OrderBy(o => o.At).ThenBy(o => o.ToId().Value, StringComparer.Ordinal)];
        var snapshot = Results.Success(found is { } f ? new MapSnapshot(f.Item1, f.Item2, overrides) : null);
        AfterSnapshot?.Invoke(draft);
        return snapshot;
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapSnapshot?>> SnapshotAt(int version, CancellationToken cancellationToken) => Done(() =>
    {
        IReadOnlyList<MapOverride> overrides = [.. _overrides.OrderBy(o => o.At).ThenBy(o => o.ToId().Value, StringComparer.Ordinal)];
        var found = _versions.Find(v => v.Version.Number == version && v.Version.Status is MapVersionStatus.Current or MapVersionStatus.Retained);
        return Results.Success(found.Graph is null ? null : new MapSnapshot(found.Version, found.Graph, overrides));
    });

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<MapOverride>>> Overrides(CancellationToken cancellationToken) =>
        Done(() => Results.Success<IReadOnlyList<MapOverride>>([.. _overrides.OrderBy(o => o.At).ThenBy(o => o.ToId().Value, StringComparer.Ordinal)]));

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> SaveCurrent(MapImport map, DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Heads is { } named && MapHead.Check(named) is { } refused)
        {
            return Results.Failure<MapVersion>(refused);
        }

        for (var i = 0; i < _versions.Count; i++)
        {
            if (_versions[i].Version.Status == MapVersionStatus.Current)
            {
                _versions[i] = _versions[i] with { Version = _versions[i].Version with { Status = MapVersionStatus.Retained } };
            }
        }

        var version = new MapVersion(++_last, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Current) { Ruleset = map.Ruleset };
        _versions.Add((version, map.Graph));
        _heads[version.Number] = map.Heads ?? (MapHead.IsCommit(map.Commit) ? [new MapHead(map.Repo, map.Commit!, OnDefault: true)] : []);
        (_hold, _chain) = (null, null);
        _ = Append(map.Overrides);
        return Results.Success(version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken) =>
        Done(() =>
        {
            var gone = _versions.Where(v => versions.Contains(v.Version.Number) && v.Version.Status == MapVersionStatus.Retained).Select(v => v.Version.Number).ToList();
            gone.ForEach(n => _ = _heads.Remove(n));
            return Results.Success(_versions.RemoveAll(v => gone.Contains(v.Version.Number)));
        });

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> SaveDraft(MapImport map, DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(map);
        if (SaveDraftFailure is { } failed)
        {
            return Results.Failure<MapVersion>(failed);
        }

        if (map.Heads is { } named && MapHead.Check(named) is { } refused)
        {
            return Results.Failure<MapVersion>(refused);
        }

        _ = DropDrafts(v => true);
        var version = new MapVersion(++_last, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Draft) { Ruleset = map.Ruleset };
        _versions.Add((version, map.Graph));
        _heads[version.Number] = map.Heads ?? [];
        _ = Append(map.Overrides);
        return Results.Success(version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> ReplaceDraft(int draft, MapImport map, DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(map);
        if (ReplaceDraftFailure is { } failed)
        {
            return Results.Failure<MapVersion>(failed);
        }

        if (map.Heads is { } named && MapHead.Check(named) is { } refused)
        {
            return Results.Failure<MapVersion>(refused);
        }

        var copied = _heads.GetValueOrDefault(draft) ?? [];
        if (DropDrafts(v => v.Number == draft) == 0)
        {
            return Results.Failure<MapVersion>(ContextErrors.PromoteRefused.With("the draft is no longer the open draft; nothing was replaced"));
        }

        var version = new MapVersion(++_last, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Draft) { Ruleset = map.Ruleset };
        _versions.Add((version, map.Graph));
        _heads[version.Number] = map.Heads ?? copied;
        _ = Append(map.Overrides);
        return Results.Success(version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> OpenDraft(DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        if (_versions.Select(v => v.Version).FirstOrDefault(v => v.Status == MapVersionStatus.Draft) is { } open)
        {
            return Results.Success(open);
        }

        if (_versions.FirstOrDefault(v => v.Version.Status == MapVersionStatus.Current) is not { Version: { } current } source)
        {
            return Results.Failure<MapVersion>(ContextErrors.NoMap.With("there is no current map to open a draft from (fix: sindri context map import <file>)"));
        }

        var copy = current with { Number = ++_last, Created = created, Status = MapVersionStatus.Draft };
        _versions.Add((copy, source.Graph));
        _heads[copy.Number] = _heads.GetValueOrDefault(current.Number) ?? [];
        return Results.Success(copy);
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> Promote(int draft, CancellationToken cancellationToken) => Done(() =>
    {
        if (PromoteFailure is { } failed)
        {
            return Results.Failure<MapVersion>(failed);
        }

        var index = _versions.FindIndex(v => v.Version.Number == draft && v.Version.Status == MapVersionStatus.Draft);
        if (index < 0)
        {
            return Results.Failure<MapVersion>(ContextErrors.PromoteRefused.With("that version isn't a draft"));
        }

        for (var i = 0; i < _versions.Count; i++)
        {
            if (_versions[i].Version.Status == MapVersionStatus.Current)
            {
                _versions[i] = _versions[i] with { Version = _versions[i].Version with { Status = MapVersionStatus.Retained } };
            }
        }

        _versions[index] = _versions[index] with { Version = _versions[index].Version with { Status = MapVersionStatus.Current } };
        (_hold, _chain) = (null, null);
        return Results.Success(_versions[index].Version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<MapHead>>> Heads(int version, CancellationToken cancellationToken) =>
        Done(() => Results.Success(_heads.GetValueOrDefault(version) ?? []));

    /// <inheritdoc/>
    public ValueTask<Result<MapPublishState>> Held(CancellationToken cancellationToken) => Done(() =>
    {
        var draft = LatestDraft();
        var current = _versions.Select(v => v.Version).FirstOrDefault(v => v.Status == MapVersionStatus.Current)?.Number;
        var hold = _hold is { } held && _chain is { } since ? new MapHold(held, since, _heads.GetValueOrDefault(held) ?? []) : null;
        return Results.Success(new MapPublishState(hold, draft, current, current is { } c ? _heads.GetValueOrDefault(c) ?? [] : []) { CurrentRuleset = _versions.Select(v => v.Version).FirstOrDefault(v => v.Status == MapVersionStatus.Current)?.Ruleset });
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapHold>> Hold(MapHold hold, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(hold);
        if (MapHead.Check(hold.Heads) is { } refused)
        {
            return Results.Failure<MapHold>(refused);
        }

        var latest = LatestDraft();
        if (latest != hold.Version)
        {
            return Results.Failure<MapHold>(ContextErrors.DraftChanged.With(Changed(hold.Version, latest)));
        }

        _chain = MapHold.NextSince(_chain, hold.Since);
        _hold = hold.Version;
        return Results.Success(new MapHold(hold.Version, _chain.Value, _heads.GetValueOrDefault(hold.Version) ?? []));
    });

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Release(int version, CancellationToken cancellationToken) => Done(() =>
    {
        _hold = _hold == version ? null : _hold;
        return Results.Success(Unit.Value);
    });

    /// <summary>The words of a 190, as the SQLite store words it.</summary>
    internal static string Changed(int expected, int? latest) => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"draft {expected} was expected, {(latest is { } n ? $"the latest draft is {n}" : "there is no draft")} (a rebuild replaced it, or it was promoted)");

    private int DropDrafts(Func<MapVersion, bool> which)
    {
        var gone = _versions.Where(v => v.Version.Status == MapVersionStatus.Draft && which(v.Version)).Select(v => v.Version.Number).ToList();
        foreach (var n in gone)
        {
            _ = _heads.Remove(n);
            _hold = _hold == n ? null : _hold;   // the hold goes with its draft; the chain stays
        }

        return _versions.RemoveAll(v => gone.Contains(v.Version.Number));
    }

    /// <inheritdoc/>
    public ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken, int? expectedDraft = null) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (expectedDraft is { } expected)
        {
            var latest = LatestDraft();
            if (latest != expected)
            {
                return Results.Failure<int>(ContextErrors.DraftChanged.With(Changed(expected, latest)));
            }
        }

        return Results.Success(Append(overrides));
    });

    /// <inheritdoc/>
    public ValueTask<Result<int>> SaveRun(ScopeProposal proposal, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(proposal);
        _runs[proposal.Run] = proposal;
        return Results.Success(proposal.Surfaces.Count);
    });

    /// <inheritdoc/>
    public ValueTask<Result<int>> Propose(ScopeProposal proposal, IReadOnlyList<MapOverride> rows, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(rows);
        var added = Append(rows);
        _runs[proposal.Run] = proposal;
        return Results.Success(added);
    });

    /// <inheritdoc/>
    public ValueTask<Result<ScopeProposal?>> Run(string run, CancellationToken cancellationToken) =>
        Done(() => Results.Success(_runs.TryGetValue(run, out var saved) ? saved : null));

    private int Append(IReadOnlyList<MapOverride> overrides)
    {
        var added = 0;
        var held = _overrides.Select(e => e.ToId().Value).ToHashSet(StringComparer.Ordinal);   // a set: a 50k-row append compared every pair
        foreach (var o in overrides.Where(o => held.Add(o.ToId().Value)))
        {
            _overrides.Add(o);
            added++;
        }

        return added;
    }

    private ValueTask<Result<T>> Done<T>(Func<Result<T>> work) => ValueTask.FromResult(Failure is { } failure ? failure : work());

    private int? LatestDraft() => _versions.Where(v => v.Version.Status == MapVersionStatus.Draft).Max(v => (int?)v.Version.Number);
}
