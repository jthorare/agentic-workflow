using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>GET /sindri/context/pins (concept pins spec §3.6 to §3.8): the live profile's pins against the approved profile, by the profile diff, and the map's rebuild state.</summary>
public sealed class GetContextPinsServiceTests
{
    private const string Human = """{"docs":{"globs":["docs/**/*.md"]}}""";
    private const string Sessions = """{"sessions":{"enabled":true}}""";
    private static readonly DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConceptSettings Pin(string name, string? id = null, string? summary = null, string[]? aliases = null, string[]? features = null, string[]? paths = null) =>
        new(id is null ? null : ConceptPinId.From(id), name, summary, features, paths, DependsOn: null, Owns: null, aliases);

    private static LoadedProfile Loaded(string context, ConceptSettings[] pins, string seed, params RepoEntry[] repos)
    {
        var profile = SliceHarness.Profile("5s").Profile with
        {
            Context = JsonDocument.Parse(context).RootElement.Clone(),
            Index = new IndexSettings(Concepts: pins),
            Repos = repos.Length == 0 ? null : repos,
        };
        return new LoadedProfile(profile, ProfileHashing.Of(System.Text.Encoding.UTF8.GetBytes(seed)), "/home/profile.yaml");
    }

    private async Task<Result<ContextPinsView>> Read(LoadedProfile approved, LoadedProfile live, string? repo = null)
    {
        _h.Clock.SetUtcNow(_now);
        _engine.Heads = _engine.Heads.Count == 0 ? [new RepoHead(ContextRepoName.From("alpha"), "1", OnDefault: true)] : _engine.Heads;
        _ = await _h.Profiles.Save(approved, Ct);
        _h.Profiles.Live = live;
        var scopes = new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts());
        var service = new GetContextPinsService(scopes, _engine.Proxy, _h.Profiles, _h.Profiles, new MapRebuildState(scopes, () => _engine.Proxy, _jobs, _h.Clock), _jobs);
        return await service.Handle(new GetContextPinsRequest(repo), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pins_read_lists_a_pin_the_live_profile_added_as_approved()
    {
        var billing = Pin("Billing", features: ["feature:invoices"]);
        var approved = Loaded(Human, [billing], "approved");
        var live = Loaded(Human, [billing, Pin("Refunds", id: "refunds", summary: "Money back.", aliases: ["chargeback"])], "live");

        var read = await Read(approved, live);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        Assert.Equal([("billing", "approved"), ("refunds", "approved")], read.Value.Pins.Select(p => (p.Id, p.State)));
        Assert.Equal(live.Hash.Value, read.Value.FileHash);   // the hash of the one file load the pins came from
        Assert.Equal(["chargeback"], read.Value.Pins[1].Aliases);
        Assert.Equal(["feature:invoices"], read.Value.Pins[0].Features);
        Assert.Equal("profile", read.Value.Pins[0].SetIn);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_without_an_id_gets_the_slug_of_its_name_and_counts_its_folder_rules()
    {
        var folders = Pin("Self scheduling", paths: ["src/shifts/**", "src/swaps/**"]);
        var profile = Loaded(Human, [folders], "same");

        var read = await Read(profile, profile);

        var row = Assert.Single(read.Value.Pins);
        Assert.Equal(("self-scheduling", 2, "approved"), (row.Id, row.FolderRules, row.State));
        Assert.Null(row.Change);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_the_profiles_pins_and_its_own_repos_only()
    {
        var repos = new[] { new RepoEntry("alpha", "/r/alpha", new IndexSettings(Concepts: [Pin("Alpha only", id: "alpha-only")])), new RepoEntry("beta", "/r/beta", new IndexSettings(Concepts: [Pin("Beta only", id: "beta-only")])) };
        var profile = Loaded(Sessions, [Pin("Shared", id: "shared")], "same", repos);
        using var caller = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("alpha").Build());

        var read = await Read(profile, profile);
        var other = await Read(profile, profile, repo: "beta");

        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        Assert.Equal("alpha", read.Value.Repo);
        Assert.Equal(["shared", "alpha-only"], read.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal("repo alpha", read.Value.Pins[1].SetIn);
        Assert.Equal(ContextErrors.RepoOutsideScope.Code, other.Error.Code);   // naming another repo is refused, never answered
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_is_refused_when_sessions_are_off()
    {
        var profile = Loaded("""{"sessions":{"enabled":false}}""", [Pin("Shared", id: "shared")], "same", new RepoEntry("alpha", "/r/alpha"));
        using var caller = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("alpha").Build());

        var read = await Read(profile, profile);

        Assert.Equal("SND-CONTEXT-020", read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_with_two_repos_reads_every_pin_and_naming_one_cuts_to_it()
    {
        var repos = new[] { new RepoEntry("alpha", "/r/alpha", new IndexSettings(Concepts: [Pin("Alpha only", id: "alpha-only")])), new RepoEntry("beta", "/r/beta", new IndexSettings(Concepts: [Pin("Beta only", id: "beta-only")])) };
        var profile = Loaded(Human, [Pin("Shared", id: "shared")], "same", repos);

        var all = await Read(profile, profile);
        var beta = await Read(profile, profile, repo: "beta");

        Assert.Null(all.Value.Repo);
        Assert.Equal(["shared", "alpha-only", "beta-only"], all.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal("beta", beta.Value.Repo);
        Assert.Equal(["shared", "beta-only"], beta.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_map_is_stale_when_it_was_built_under_other_rules_and_carries_its_version()
    {
        var profile = Loaded(Human, [Pin("Billing", id: "billing")], "same");
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: []) { CurrentRuleset = "built-under-other-pins" };

        var stale = await Read(profile, profile);
        _engine.State = _engine.State with { CurrentRuleset = MapRuleset.Running(ContextScopes.For(profile.Profile, ContextScopes.Caller(caller: null))) };
        var current = await Read(profile, profile);

        Assert.Equal(("v4", true), (stale.Value.MapVersion, stale.Value.Rebuild.Stale));
        Assert.False(current.Value.Rebuild.Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_map_has_no_version_and_is_not_stale()
    {
        var profile = Loaded(Human, [], "same");

        var read = await Read(profile, profile);

        Assert.Equal((null, false, "idle"), (read.Value.MapVersion, read.Value.Rebuild.Stale, read.Value.Rebuild.Phase));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_rebuild_phase_comes_from_the_refresh_jobs_and_the_heads()
    {
        var profile = Loaded(Human, [], "same");
        _engine.Heads = [new RepoHead(ContextRepoName.From("alpha"), "1", OnDefault: false)];
        var none = await Read(profile, profile);
        _engine.Heads = [new RepoHead(ContextRepoName.From("alpha"), "1", OnDefault: true)];
        Refresh(JobState.Failed, TimeSpan.FromMinutes(1), 1, "SND-CONTEXT-030");
        var backoff = await Read(profile, profile);
        Refresh(JobState.Running, TimeSpan.Zero, 2);
        var building = await Read(profile, profile);

        Assert.Equal("noMainBranch", none.Value.Rebuild.Phase);
        Assert.Equal("backoff", backoff.Value.Rebuild.Phase);
        Assert.True(backoff.Value.Rebuild.RetryAt is not null);
        Assert.Equal("building", building.Value.Rebuild.Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_refresh_is_named_until_a_later_one_succeeds()
    {
        var profile = Loaded(Human, [], "same");
        Refresh(JobState.Succeeded, TimeSpan.FromHours(3), 1);
        Refresh(JobState.Failed, TimeSpan.FromHours(2), 2, "SND-CONTEXT-030");
        var failed = await Read(profile, profile);
        Refresh(JobState.Succeeded, TimeSpan.FromHours(1), 3);

        var recovered = await Read(profile, profile);

        Assert.Equal("SND-CONTEXT-030", failed.Value.Rebuild.Failed);
        Assert.Null(recovered.Value.Rebuild.Failed);
    }

    // The approved profile pins billing and refunds on one repo; the map was built with the hashes the test hands over (null: a map from before migration 0028).
    private async Task<IReadOnlyList<(string Id, string State, string? Change)>> Changes(Func<IReadOnlyDictionary<string, string>, Dictionary<string, string>?> built, LoadedProfile? live = null)
    {
        var approved = Loaded(Human, [Pin("Billing", id: "billing"), Pin("Refunds", id: "refunds")], "approved", new RepoEntry("alpha", "/r/alpha"));
        var hashes = MapRuleset.RunningPins(ContextScopes.For(approved.Profile, ContextScopes.Caller(caller: null)))!;
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: [new MapHead(ContextRepoName.From("alpha"), "1", OnDefault: true)]) { CurrentRuleset = "r", CurrentPinHashes = built(hashes) };

        var read = await Read(approved, live ?? approved);

        return [.. read.Value.Pins.Select(p => (p.Id, p.State, p.Change))];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_the_map_was_built_with_unchanged_has_no_change() =>
        Assert.Equal([("billing", "approved", null), ("refunds", "approved", null)], await Changes(h => new Dictionary<string, string>(h, StringComparer.Ordinal)));

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_pin_the_map_has_no_hash_for_is_created() =>
        Assert.Equal([("billing", "approved", null), ("refunds", "approved", "created")], await Changes(h => h.Where(p => p.Key == "billing").ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)));

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_pin_whose_hash_differs_from_the_maps_is_updated() =>
        Assert.Equal([("billing", "approved", "updated"), ("refunds", "approved", null)], await Changes(h => new Dictionary<string, string>(h, StringComparer.Ordinal) { ["billing"] = "built-with-another-version" }));

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_the_map_has_that_the_approved_profile_lacks_is_listed_after_the_live_pins_as_removed() =>
        Assert.Equal([("billing", "approved", null), ("refunds", "approved", null), ("ghost", "approved", "removed")], await Changes(h => new Dictionary<string, string>(h, StringComparer.Ordinal) { ["ghost"] = "gone" }));

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_with_no_per_pin_hashes_leaves_every_change_unknown() =>
        Assert.Equal([("billing", "approved", null), ("refunds", "approved", null)], await Changes(_ => null));

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_edit_and_a_new_live_pin_are_read_from_the_live_profile()
    {
        var live = Loaded(Human, [Pin("Billing", id: "billing", summary: "edited"), Pin("Refunds", id: "refunds"), Pin("New", id: "new")], "live", new RepoEntry("alpha", "/r/alpha"));

        var rows = await Changes(h => new Dictionary<string, string>(h, StringComparer.Ordinal), live);

        Assert.Equal([("billing", "approved", null), ("refunds", "approved", null), ("new", "approved", "created")], rows);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sessions_rebuild_carries_no_progress_line_and_a_humans_does()
    {
        var profile = Loaded(Sessions, [], "same", new RepoEntry("alpha", "/r/alpha"));
        Refresh(JobState.Running, TimeSpan.Zero, 1, code: null, "retrying (attempt 2 of 4) in 30 s after SND-CONTEXT-030: could not read /r/beta");
        var human = await Read(profile, profile);
        using var caller = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("alpha").Build());

        var session = await Read(profile, profile);

        Assert.Contains("beta", human.Value.Rebuild.Progress, StringComparison.Ordinal);
        Assert.Equal(("building", null), (session.Value.Rebuild.Phase, session.Value.Rebuild.Progress));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_id_less_pins_that_slug_alike_are_foo_and_foo_2_each_with_its_own_change()
    {
        var profile = Loaded(Human, [Pin("Foo", features: ["feature:a"]), Pin("Foo", features: ["feature:b"])], "same", new RepoEntry("alpha", "/r/alpha"));
        var hashes = new Dictionary<string, string>(MapRuleset.RunningPins(ContextScopes.For(profile.Profile, ContextScopes.Caller(caller: null)))!, StringComparer.Ordinal) { ["foo-2"] = "built-with-another-version" };
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: []) { CurrentRuleset = "r", CurrentPinHashes = hashes };

        var read = await Read(profile, profile);

        Assert.Equal(["foo", "foo-2"], read.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal([null, "updated"], read.Value.Pins.Select(p => p.Change), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_only_the_map_has_is_listed_for_an_answer_over_every_repo_and_never_for_one_repo()
    {
        var repos = new[] { new RepoEntry("alpha", "/r/alpha", new IndexSettings(Concepts: [Pin("Alpha only", id: "alpha-only")])), new RepoEntry("beta", "/r/beta") };
        var profile = Loaded(Sessions, [], "same", repos);
        var hashes = new Dictionary<string, string>(MapRuleset.RunningPins(ContextScopes.For(profile.Profile, ContextScopes.Caller(caller: null)))!, StringComparer.Ordinal) { ["refunds"] = "another repo's pin" };
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: []) { CurrentRuleset = "r", CurrentPinHashes = hashes };

        var human = await Read(profile, profile);
        var humanOneRepo = await Read(profile, profile, repo: "alpha");
        using var caller = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("alpha").Build());
        var session = await Read(profile, profile);

        Assert.Equal(["alpha-only", "refunds"], human.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal(["alpha-only"], humanOneRepo.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal(["alpha-only"], session.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_of_a_repo_the_profile_dropped_is_not_listed_until_the_map_is_rebuilt()
    {
        var profile = Loaded(Sessions, [], "same", new RepoEntry("alpha", "/r/alpha", new IndexSettings(Concepts: [Pin("Alpha only", id: "alpha-only")])));
        var hashes = new Dictionary<string, string>(MapRuleset.RunningPins(ContextScopes.For(profile.Profile, ContextScopes.Caller(caller: null)))!, StringComparer.Ordinal) { ["refunds"] = "beta's pin" };
        var alpha = new MapHead(ContextRepoName.From("alpha"), "1", OnDefault: true);
        var beta = new MapHead(ContextRepoName.From("beta"), "2", OnDefault: true);
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: [alpha, beta]) { CurrentRuleset = "r", CurrentPinHashes = hashes };

        var humanOneRepo = await Read(profile, profile, repo: "alpha");
        using var caller = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("alpha").Build());
        var session = await Read(profile, profile);

        Assert.Equal(["alpha-only"], humanOneRepo.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal(["alpha-only"], session.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_over_the_profiles_only_repo_may_list_its_removed_pins()
    {
        var profile = Loaded(Sessions, [], "same", new RepoEntry("alpha", "/r/alpha", new IndexSettings(Concepts: [Pin("Alpha only", id: "alpha-only")])));
        var hashes = new Dictionary<string, string>(MapRuleset.RunningPins(ContextScopes.For(profile.Profile, ContextScopes.Caller(caller: null)))!, StringComparer.Ordinal) { ["refunds"] = "alpha's removed pin" };
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: [new MapHead(ContextRepoName.From("alpha"), "1", OnDefault: true)]) { CurrentRuleset = "r", CurrentPinHashes = hashes };

        var human = await Read(profile, profile);

        Assert.Equal(["alpha-only", "refunds"], human.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_imported_map_with_no_heads_lists_no_removed_pins_for_one_repo()
    {
        var profile = Loaded(Sessions, [], "same", new RepoEntry("alpha", "/r/alpha", new IndexSettings(Concepts: [Pin("Alpha only", id: "alpha-only")])));
        var hashes = new Dictionary<string, string>(MapRuleset.RunningPins(ContextScopes.For(profile.Profile, ContextScopes.Caller(caller: null)))!, StringComparer.Ordinal) { ["refunds"] = "a dropped repo's pin" };
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: []) { CurrentRuleset = "r", CurrentPinHashes = hashes };

        var humanOneRepo = await Read(profile, profile, repo: "alpha");
        using var caller = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("alpha").Build());
        var session = await Read(profile, profile);

        Assert.Equal(["alpha-only"], humanOneRepo.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal(["alpha-only"], session.Value.Pins.Select(p => p.Id), StringComparer.Ordinal);
    }

    // ---- Moved: what the newest refresh moved in and out of each pin, and what the relink after it did to the pin's records ----

    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private static PinMove Move(string pin, int gained, int lost, params PinMoveTo[] to) => new(ConceptId.From(pin), 41, 42, gained, lost, to);

    private void PutRefresh(int version, PinMove[]? moves, TimeSpan endedAgo, long sequence)
    {
        var result = new RefreshContextMapResult("alpha", version, new Dictionary<string, string>(StringComparer.Ordinal), 0, 0, 0, "none", Held: false) { Moves = moves };
        _jobs.Put(new JobRow(JobId.New(_h.Ids), ContextMapRefreshService.RefreshKind, JobKey.From("refresh"), Item: null, Repo: null, Service: null, JobState.Succeeded,
            "service:context-map-refresh", _now - endedAgo - TimeSpan.FromSeconds(5), _now - endedAgo, [], Rounds: 0, Summary: null, Code: null,
            JsonSerializer.SerializeToElement(result, _web), CancelledBy: null, Sequence: sequence));
    }

    // Billing is the one pin; the map is at version 42, and the refresh that published 42 ended 3 h ago with these moves (null: the result carries none).
    private async Task<ContextPinRow> Billing(int mapVersion, int refreshVersion, PinMove[]? moves)
    {
        var profile = Loaded(Human, [Pin("Billing", id: "billing")], "same", new RepoEntry("alpha", "/r/alpha"));
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, mapVersion, [new MapHead(ContextRepoName.From("alpha"), "1", OnDefault: true)]);
        PutRefresh(refreshVersion, moves, TimeSpan.FromHours(3), 1);
        var read = await Read(profile, profile);
        return Assert.Single(read.Value.Pins);
    }

    private void Relink(JobState state, TimeSpan endedAgo, long sequence, int version, int before = 0, int after = 0) =>
        _jobs.Put(new JobRow(JobId.New(_h.Ids), ContextMapRefreshService.RelinkKind, JobKey.From("relink"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", _now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null,
            state == JobState.Failed ? "SND-CONTEXT-030" : null,
            state == JobState.Succeeded ? JsonSerializer.SerializeToElement(new RelinkPinAliasesJobResult("h", version, Records: 9, Changed: 1, ElapsedMs: 5, [new PinLinkCount(ConceptId.From("billing"), before, after)]), _web) : null,
            CancelledBy: null, Sequence: sequence));

    [Fact, Trait("Lane", "unit")]
    public async Task While_the_relink_still_runs_moved_has_the_feature_counts_and_no_record_count()
    {
        Relink(JobState.Running, TimeSpan.Zero, 2, version: 42);

        var row = await Billing(42, 42, [Move("billing", 2, 3, new PinMoveTo(ConceptId.From("refunds"), "Refunds", 3))]);

        var expected = new ContextPinMoved("v41", "v42", 2, 3, [new ContextPinMovedTo("refunds", "Refunds", 3)], RecordsLinked: null);
        Assert.Equal(expected.To, row.Moved!.To);   // the list compares by reference inside the record
        Assert.Equal(expected, row.Moved with { To = expected.To });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_that_linked_more_records_is_a_positive_count()
    {
        Relink(JobState.Succeeded, TimeSpan.FromHours(2), 2, version: 42, before: 10, after: 17);

        var row = await Billing(42, 42, [Move("billing", 1, 0)]);

        Assert.Equal((7, false), (row.Moved!.RecordsLinked, row.Moved.RelinkFailed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_that_linked_fewer_records_is_a_negative_count()
    {
        Relink(JobState.Succeeded, TimeSpan.FromHours(2), 2, version: 42, before: 17, after: 12);

        var row = await Billing(42, 42, [Move("billing", 0, 1)]);

        Assert.Equal(-5, row.Moved!.RecordsLinked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_relinks_of_the_version_after_the_refresh_add_up()
    {
        Relink(JobState.Succeeded, TimeSpan.FromHours(2), 2, version: 42, before: 10, after: 17);
        Relink(JobState.Succeeded, TimeSpan.FromHours(1), 3, version: 42, before: 17, after: 18);

        var row = await Billing(42, 42, [Move("billing", 1, 0)]);

        Assert.Equal(8, row.Moved!.RecordsLinked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_that_failed_after_a_success_sets_relink_failed_and_no_record_count()
    {
        Relink(JobState.Succeeded, TimeSpan.FromHours(2), 2, version: 42, before: 10, after: 17);
        Relink(JobState.Failed, TimeSpan.FromMinutes(90), 3, version: 42);

        var row = await Billing(42, 42, [Move("billing", 1, 0)]);

        Assert.True(row.Moved!.RelinkFailed);
        Assert.Null(row.Moved.RecordsLinked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_success_after_a_failed_relink_clears_it_and_the_count_is_the_sum_of_every_success()
    {
        Relink(JobState.Succeeded, TimeSpan.FromHours(2), 2, version: 42, before: 10, after: 17);   // +7
        Relink(JobState.Failed, TimeSpan.FromMinutes(90), 3, version: 42);
        Relink(JobState.Succeeded, TimeSpan.FromHours(1), 4, version: 42, before: 17, after: 18);   // +1

        var row = await Billing(42, 42, [Move("billing", 1, 0)]);

        Assert.Equal((false, 8), (row.Moved!.RelinkFailed, row.Moved.RecordsLinked));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_newer_refresh_whose_draft_was_held_does_not_hide_the_moves_of_the_one_that_published()
    {
        PutRefresh(version: 43, moves: null, TimeSpan.FromHours(1), 5);   // succeeded, held: the current map is still 42

        var row = await Billing(42, 42, [Move("billing", 2, 1)]);

        Assert.Equal((2, 1), (row.Moved!.Gained, row.Moved.Lost));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_newer_refresh_that_ended_already_current_does_not_hide_the_moves_of_the_one_that_published()   // also where the retry after a recorder error ends
    {
        PutRefresh(version: 42, moves: null, TimeSpan.FromHours(1), 5);

        var row = await Billing(42, 42, [Move("billing", 2, 1)]);

        Assert.Equal((2, 1), (row.Moved!.Gained, row.Moved.Lost));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_of_another_version_does_not_count()
    {
        Relink(JobState.Succeeded, TimeSpan.FromHours(2), 2, version: 41, before: 1, after: 9);

        var row = await Billing(42, 42, [Move("billing", 0, 0)]);

        Assert.Null(row.Moved!.RecordsLinked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_that_failed_after_the_refresh_sets_relink_failed_and_no_record_count()
    {
        Relink(JobState.Failed, TimeSpan.FromHours(2), 2, version: 42);

        var row = await Billing(42, 42, [Move("billing", 1, 1)]);

        Assert.True(row.Moved!.RelinkFailed);
        Assert.Null(row.Moved.RecordsLinked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_later_successful_relink_clears_the_failure()
    {
        Relink(JobState.Failed, TimeSpan.FromHours(2), 2, version: 42);
        Relink(JobState.Succeeded, TimeSpan.FromHours(1), 3, version: 42, before: 4, after: 6);

        var row = await Billing(42, 42, [Move("billing", 1, 1)]);

        Assert.Equal((false, 2), (row.Moved!.RelinkFailed, row.Moved.RecordsLinked));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_that_failed_before_the_refresh_ended_is_not_this_versions_failure()
    {
        Relink(JobState.Failed, TimeSpan.FromHours(4), 0, version: 41);

        var row = await Billing(42, 42, [Move("billing", 1, 1)]);

        Assert.False(row.Moved!.RelinkFailed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_current_map_other_than_the_refreshs_version_has_no_moved()
    {
        var row = await Billing(43, 42, [Move("billing", 1, 1)]);

        Assert.Null(row.Moved);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_with_no_move_entry_in_a_refresh_that_stored_moves_has_no_moved()
    {
        var row = await Billing(42, 42, [Move("refunds", 5, 5)]);

        Assert.Null(row.Moved);
    }

    [Fact, Trait("Lane", "unit")]
    public void Moved_is_left_out_when_null_and_relink_failed_when_false_and_the_names_are_camel_case()
    {
        var plain = new ContextPinRow("billing", "Billing", Summary: null, [], [], FolderRules: 0, "profile", PinStates.Approved, Change: null);
        var moved = plain with { Moved = new ContextPinMoved("v41", "v42", 1, 2, [new ContextPinMovedTo(Id: null, Name: null, 2)], RecordsLinked: -3) };
        var failed = plain with { Moved = new ContextPinMoved("v41", "v42", 0, 0, [], RecordsLinked: null) { RelinkFailed = true } };

        using var none = JsonDocument.Parse(JsonSerializer.Serialize(plain, SliceHarness.Json));
        using var shown = JsonDocument.Parse(JsonSerializer.Serialize(moved, SliceHarness.Json));
        using var broken = JsonDocument.Parse(JsonSerializer.Serialize(failed, SliceHarness.Json));

        Assert.False(none.RootElement.TryGetProperty("moved", out _));
        var m = shown.RootElement.GetProperty("moved");
        Assert.Equal(["fromVersion", "toVersion", "gained", "lost", "to", "recordsLinked"], m.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(-3, m.GetProperty("recordsLinked").GetInt32());
        Assert.Equal(2, m.GetProperty("to")[0].GetProperty("features").GetInt32());
        Assert.True(broken.RootElement.GetProperty("moved").GetProperty("relinkFailed").GetBoolean());
    }

    private void Refresh(JobState state, TimeSpan endedAgo, long sequence, string? code = null, params string[] lines) =>
        _jobs.Put(new JobRow(JobId.New(_h.Ids), ContextMapRefreshService.RefreshKind, JobKey.From("refresh"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", _now - endedAgo - TimeSpan.FromSeconds(5), _now - endedAgo, [.. lines.Select((l, i) => new JobLine(i + 1, l, _now))], Rounds: 0, Summary: null, code, Result: null, CancelledBy: null, Sequence: sequence));

    [Fact, Trait("Lane", "unit")]
    public async Task An_invalid_live_profile_file_reads_as_no_pins_with_an_error_line_naming_the_file()
    {
        _h.Clock.SetUtcNow(_now);
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");
        var scopes = new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts());
        var service = new GetContextPinsService(scopes, _engine.Proxy, _h.Profiles, _h.Profiles, new MapRebuildState(scopes, () => _engine.Proxy, _jobs, _h.Clock), _jobs);

        var read = await service.Handle(new GetContextPinsRequest(null), Ct);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Empty(read.Value.Pins);
        Assert.Contains("/home/thor/profile.yaml", read.Value.ProfileError, StringComparison.Ordinal);
    }
}
