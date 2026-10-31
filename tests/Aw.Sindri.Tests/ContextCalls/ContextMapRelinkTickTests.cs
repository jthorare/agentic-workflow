using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Concept pins spec 3.4, 3.7: the 'context-map-refresh' tick queues a RelinkPinAliases job when the pins' aliases or the current map version differ from the last relink that succeeded, and only when no refresh, publish or relink runs.</summary>
public sealed class ContextMapRelinkTickTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly RefreshStarter _refresh;
    private readonly RelinkStarter _relink;
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();
    private Core.Lease.LeaseGrant? _grant;
    private int _next;

    public ContextMapRelinkTickTests()
    {
        _refresh = new RefreshStarter(_h);
        _relink = new RelinkStarter(_h);
        _engine.Heads = [new RepoHead(ContextRepoName.From("a"), "1", OnDefault: true)];
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void MapAt(int version) =>
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, version, [new MapHead(ContextRepoName.From("a"), "1", OnDefault: true)]);

    private void Relinked(string hash, int version, JobState state = JobState.Succeeded, TimeSpan? endedAgo = null) =>
        RelinkRig.Put(_jobs, _h, _now, state, endedAgo ?? TimeSpan.FromHours(1), hash, version, ++_next);

    // The refresh that built the map (so the move check has nothing to start), or one that still runs.
    private void Refresh(JobState state) =>
        _jobs.Put(new JobRow(JobId.New(_h.Ids), JobKind.From("RefreshContextMap"), JobKey.From("refresh"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", _now - TimeSpan.FromHours(1), state == JobState.Running ? null : _now - TimeSpan.FromMinutes(59), [], Rounds: 0, Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: ++_next));

    private void Publish(JobState state) =>
        _jobs.Put(new JobRow(JobId.New(_h.Ids), JobKind.From("PublishContextMap"), JobKey.From("map-publish"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", _now - TimeSpan.FromHours(1), state == JobState.Running ? null : _now - TimeSpan.FromMinutes(59), [], Rounds: 0, Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: ++_next));

    // A profile that maps repo 'a' and pins one concept, 'payroll', with these aliases.
    private static LoadedProfile Pinned(params string[] aliases)
    {
        var loaded = Plain();
        return loaded with { Profile = loaded.Profile with { Repos = [new RepoEntry("a", "/work/a")], Index = new IndexSettings(Concepts: [new ConceptSettings(ConceptPinId.From("payroll"), "Payroll", Aliases: aliases)]) } };
    }

    private static LoadedProfile Plain()
    {
        var loaded = SliceHarness.Profile("5s");
        return loaded with { Profile = loaded.Profile with { Context = JsonDocument.Parse("""{"docs":{"globs":["docs/**/*.md"]}}""").RootElement.Clone() } };
    }

    private async Task<Result<ScheduledResult>> Tick(LoadedProfile? profile = null)
    {
        _h.Clock.SetUtcNow(_now);
        _ = await _h.Profiles.Save(profile ?? Plain(), Ct);
        var service = new ContextMapRefreshService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _jobs, new ScriptedOutlooks(), new MapJobStarts(_refresh, new PublishStarter(_h), _relink), _h.Clock, NullLogger<ContextMapRefreshService>.Instance);
        _grant ??= await _h.HoldLease();
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Run(new ScheduledRun(_grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_aliases_hash_queues_a_relink_as_the_service_and_no_refresh()
    {
        MapAt(3);
        Relinked("a hash from before the aliases were edited", version: 3);

        var result = await Tick();

        Assert.False(result.Value.Idle);
        Assert.Equal("service:context-map-refresh", Assert.Single(_relink.Actors));
        Assert.Empty(_refresh.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_alias_only_profile_edit_queues_a_relink_and_no_refresh()
    {
        MapAt(3);
        var before = Pinned("pay run");
        var hash = PinAliasSet.Hash(PinAliasSet.Of(ContextScopes.For(before.Profile, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli))).Value);
        Relinked(hash, version: 3);

        var unchanged = await Tick(before);
        var edited = await Tick(Pinned("pay run", "payroll run"));

        Assert.True(unchanged.Value.Idle);
        Assert.False(edited.Value.Idle);
        _ = Assert.Single(_relink.Actors);
        Assert.Empty(_refresh.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_map_version_queues_a_relink()
    {
        MapAt(4);
        Relinked(RelinkRig.NoPinsHash, version: 3);

        var result = await Tick();

        Assert.False(result.Value.Idle);
        _ = Assert.Single(_relink.Actors);
        Assert.Empty(_refresh.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_relink_yet_on_a_map_that_exists_queues_one()
    {
        MapAt(3);

        var result = await Tick();

        Assert.False(result.Value.Idle);
        _ = Assert.Single(_relink.Actors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_pair_as_the_last_succeeded_relink_queues_nothing()
    {
        MapAt(3);
        Relinked(RelinkRig.NoPinsHash, version: 3);

        var result = await Tick();

        Assert.True(result.Value.Idle);
        Assert.Empty(_relink.Actors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_relink_after_the_matching_success_does_not_count_and_a_newer_success_decides()
    {
        MapAt(3);
        Relinked("old hash", version: 3, endedAgo: TimeSpan.FromHours(3));
        Relinked(RelinkRig.NoPinsHash, version: 3, endedAgo: TimeSpan.FromHours(2));
        Relinked("never stored", version: 3, JobState.Failed, TimeSpan.FromHours(1));

        _ = await Tick();

        Assert.Empty(_relink.Actors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_map_at_all_queues_no_relink()
    {
        _ = await Tick();

        Assert.Empty(_relink.Actors);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("refresh")]
    [InlineData("publish")]
    [InlineData("relink")]
    public async Task A_running_refresh_publish_or_relink_holds_the_relink_back(string running)
    {
        MapAt(3);
        Relinked("old hash", version: 3);
        Refresh(JobState.Succeeded);
        switch (running)
        {
            case "refresh": Refresh(JobState.Running); break;
            case "publish": Publish(JobState.Running); break;
            default: Relinked("old hash", version: 3, JobState.Running); break;
        }

        _ = await Tick();

        Assert.Empty(_relink.Actors);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_that_just_failed_backs_off_before_it_is_tried_again()
    {
        MapAt(3);
        Relinked("old hash", version: 3, JobState.Failed, TimeSpan.FromMinutes(1));   // the first wait is 5 minutes

        var result = await Tick();

        Assert.True(result.Value.Idle);
        Assert.Empty(_relink.Actors);
    }

    private sealed class RefreshStarter(SliceHarness harness) : IService<RefreshContextMapRequest, JobStartedResponse>
    {
        internal List<RefreshContextMapRequest> Requests { get; } = [];

        public ValueTask<Result<JobStartedResponse>> Handle(RefreshContextMapRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult<Result<JobStartedResponse>>(new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }

    private sealed class PublishStarter(SliceHarness harness) : IService<PublishContextMapRequest, JobStartedResponse>
    {
        public ValueTask<Result<JobStartedResponse>> Handle(PublishContextMapRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<JobStartedResponse>>(new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
    }
}
