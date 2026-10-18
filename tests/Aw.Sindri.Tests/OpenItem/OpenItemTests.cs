using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Features.OpenItem;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.OpenItem;

/// <summary>Opening an item (UI spec E-1, API spec §3.2): one 'item.open-requested', after the checks; the launcher service does the launching.</summary>
[Lane(TestLane.Unit)]
public sealed class OpenItemTests : IDisposable
{
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly AgentId _codex = AgentId.From("codex");
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _aider = AgentId.From("aider");
    private static readonly LaunchId _earlier = LaunchId.From("LCH-" + new string('0', 31) + "9");

    private readonly ViewsHarness _h = new();
    private readonly FixedLaunchSpend _spend = new();
    private readonly OpenRequests _requests;

    public OpenItemTests()
    {
        _requests = new OpenRequests(_h.Slice.Clock);
    }

    public void Dispose() => _requests.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private OpenItemService Service(params AgentEntry[] catalog)
    {
        var launches = new LaunchViews(_h.Views);
        return new(new OpenChecks(new ViewItemResolver(_h.Views), launches, _requests, _h.Slice.Clock),
            new OpenTarget(_h.Slice.Profiles, launches, new AgentCatalog(catalog.Length == 0 ? [AgentEntries.ClaudeCode, AgentEntries.Codex] : catalog), _spend),
            _h.Slice.Held, _h.Slice.Store, _h.Slice.Events, _h.Slice.Ids, _h.Slice.Callers);
    }

    private async Task Seed(RepoName? repo = null)
    {
        _ = await _h.Slice.HoldLease();
        await _h.Item(repo ?? _repo);
    }

    private async Task Approve(LaunchSettings? launch = null, AgentSettings? agents = null)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Launch: launch, Agents: agents)).Value;
        _ = await _h.Slice.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    private static AgentSettings Budget(decimal usd, AgentId? target = null) =>
        new(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [(target ?? _claude).Value] = new(DailyBudget: usd) });

    private IDisposable Human() => _h.Slice.Callers.Enter(A.Caller().Human().Build());

    private ItemOpenRequested Requested() => _h.Slice.Events.Payload<ItemOpenRequested>(Assert.Single(_h.Slice.Store.Events)).Value;

    [Fact]
    public async Task Open_appends_one_item_open_requested_and_returns_LaunchRequested()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Slice.Events.Payload<ItemOpenRequested>(recorded).Value;
        Expect.Equal(EventTypes.ItemOpenRequested, recorded.Envelope.Type);
        Expect.Equal(A.FirstItemId.Value, recorded.Item);
        Expect.Equal($"item.open-requested:{payload.Launch.Value}", recorded.IdempotencyKey);
        Expect.Equal((LaunchMode.Open, _claude, "default"), (payload.Mode, payload.Target, payload.ChosenBy));
        Expect.Equal((payload.Launch, recorded.Sequence, _claude, "default"), (result.Value.Launch, result.Value.Accepted.Sequence, result.Value.Target, result.Value.ChosenBy));
        Assert.Empty(result.Value.Warnings);
    }

    [Fact]
    public async Task A_picked_target_is_chosen_by_click_and_is_not_checked_against_the_catalog()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().WithTarget(_aider).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((_aider, "click"), (Requested().Target, Requested().ChosenBy));   // the launcher answers SND-LAUNCH-003; the click is the person's word
    }

    [Fact]
    public async Task Second_open_in_flight_is_SND_LAUNCH_008()
    {
        await Seed();
        await _h.Request(_earlier);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.LaunchInProgress.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_request_past_its_deadline_is_not_in_flight()
    {
        await Seed();
        await _h.Request(_earlier);
        _h.Slice.Clock.Advance(LaunchRules.Deadline + TimeSpan.FromSeconds(1));
        using var entered = Human();

        Assert.True((await Service().Handle(A.OpenItemRequest().Build(), Token)).IsSuccess);
    }

    [Fact]
    public async Task A_request_with_an_outcome_is_not_in_flight()
    {
        await Seed();
        await _h.Request(_earlier);
        await _h.Fail(launch: _earlier);
        using var entered = Human();

        Assert.True((await Service().Handle(A.OpenItemRequest().Build(), Token)).IsSuccess);
    }

    [Fact]
    public async Task Skipped_default_resolves_to_next_rule_with_chosenBy()
    {
        await Seed();
        await Approve(new LaunchSettings(Default: _codex, Repos: new Dictionary<string, LaunchRepoSettings>(StringComparer.Ordinal) { [_repo.Value] = new(Default: AgentId.From("cursor")) }));
        await _h.Request(_earlier, target: _aider);
        await _h.Start(_earlier, target: _aider);   // the repo's last used agent is not in this box's catalog, and neither is the repo's default 'cursor'
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((_codex, "default"), (result.Value.Target, result.Value.ChosenBy));
    }

    [Fact]
    public async Task The_repos_last_used_agent_beats_the_defaults()
    {
        await Seed();
        await Approve(new LaunchSettings(Default: _claude));
        await _h.Request(_earlier, target: _codex);
        await _h.Start(_earlier, target: _codex);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(_codex, result.Value.Target);
    }

    [Fact]
    public async Task The_repo_default_beats_the_profile_default()
    {
        await Seed();
        await Approve(new LaunchSettings(Default: _claude, Repos: new Dictionary<string, LaunchRepoSettings>(StringComparer.Ordinal) { [_repo.Value] = new(Default: _codex) }));
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(_codex, result.Value.Target);
    }

    [Fact]
    public async Task A_catalog_with_no_usable_default_is_SND_LAUNCH_003()
    {
        await Seed();
        using var entered = Human();

        var result = await Service(AgentEntries.Aider).Handle(A.OpenItemRequest().Build(), Token);   // claude-code, the last rule, is absent too

        Expect.Equal(SindriErrors.LaunchAgentMissing.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Hard_budget_refuses_010_and_writes_nothing()
    {
        await Seed();
        await Approve(agents: Budget(10m));
        _spend.Spent(_claude, 10m);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.LaunchBudgetReached.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_budget_with_no_usage_reader_is_refused_010_too()
    {
        await Seed();
        await Approve(agents: Budget(10m));
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.LaunchBudgetReached.Code, result.Error.Code);
        Assert.Contains("no usage reader", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_agent_still_opens_while_one_is_over_budget()
    {
        await Seed();
        await Approve(agents: Budget(10m));
        _spend.Spent(_claude, 99m);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().WithTarget(_codex).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }

    [Fact]
    public async Task Soft_budget_warns()
    {
        await Seed();
        await Approve(agents: Budget(10m));
        _spend.Spent(_claude, 8m);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(["budget-soft"], result.Value.Warnings);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Attach_starts_no_process_so_the_budget_does_not_gate_it()
    {
        await Seed();
        await Approve(agents: Budget(10m));
        _spend.Spent(_claude, 99m);
        await _h.Request(_earlier);
        await _h.Start(_earlier);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Attach).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }

    [Fact]
    public async Task Attach_without_a_live_session_is_SND_LAUNCH_006()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Attach).Build(), Token);

        Expect.Equal(SindriErrors.LaunchNoSession.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Attach_after_the_session_ended_is_SND_LAUNCH_006()
    {
        await Seed();
        await _h.Request(_earlier);
        await _h.Start(_earlier);
        await _h.End(launch: _earlier);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Attach).Build(), Token);

        Expect.Equal(SindriErrors.LaunchNoSession.Code, result.Error.Code);
    }

    [Fact]
    public async Task Attach_takes_the_live_sessions_agent()
    {
        await Seed();
        await _h.Request(_earlier, target: _codex);
        await _h.Start(_earlier, target: _codex);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Attach).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((LaunchMode.Attach, _codex, "default"), (Requested().Mode, Requested().Target, Requested().ChosenBy));
    }

    [Fact]
    public async Task Resume_needs_a_session_id_from_the_start_or_the_report()
    {
        await Seed();
        await _h.Request(_earlier, target: _codex);
        await _h.Start(_earlier, target: _codex);   // started with no id
        await _h.End(launch: _earlier);   // a session that still runs is attached to, not resumed
        using var entered = Human();

        var without = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Resume).Build(), Token);
        await _h.Reported(SessionId.From("reported-1"), _earlier);
        var with = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Resume).Build(), Token);

        Expect.Equal(SindriErrors.LaunchNoSession.Code, without.Error.Code);
        Assert.True(with.IsSuccess, with.IsError ? with.Error.ToString() : null);
        Expect.Equal((LaunchMode.Resume, _codex), (Requested().Mode, Requested().Target));
    }

    [Fact]
    public async Task Resume_of_a_session_that_still_runs_is_refused_and_points_at_attach()
    {
        await Seed();
        await _h.Request(_earlier, target: _codex);
        await _h.Start(_earlier, target: _codex, session: SessionId.From("sess-1"));   // started, never ended: live
        using var entered = Human();

        var live = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Resume).Build(), Token);
        await _h.End(launch: _earlier);
        var ended = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Resume).Build(), Token);

        Expect.Equal(SindriErrors.LaunchNoSession.Code, live.Error.Code);
        Assert.Contains("attach", live.Error.Message, StringComparison.Ordinal);
        Assert.True(ended.IsSuccess, ended.IsError ? ended.Error.ToString() : null);
    }

    [Fact]
    public async Task An_attach_launch_is_not_a_live_session_to_attach_to()
    {
        await Seed();
        await _h.Request(_earlier, target: _codex, mode: LaunchMode.Attach);
        await _h.Start(_earlier, target: _codex, mode: LaunchMode.Attach);   // the human attached once; nothing ever ends that row
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().InMode(LaunchMode.Attach).Build(), Token);

        Expect.Equal(SindriErrors.LaunchNoSession.Code, result.Error.Code);
    }

    [Fact]
    public async Task Two_opens_at_once_record_one_request()
    {
        await Seed();
        using var entered = Human();
        var service = Service();

        var results = await Task.WhenAll(service.Handle(A.OpenItemRequest().Build(), Token).AsTask(), service.Handle(A.OpenItemRequest().Build(), Token).AsTask());

        _ = Assert.Single(results, static r => r.IsSuccess);
        Expect.Equal(SindriErrors.LaunchInProgress.Code, Assert.Single(results, static r => r.IsError).Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task An_open_right_after_another_is_in_progress_before_the_view_shows_the_first()
    {
        await Seed();
        using var entered = Human();
        var first = await Service().Handle(A.OpenItemRequest().Build(), Token);   // appended; the launches view has not folded it yet

        var second = await Service().Handle(A.OpenItemRequest().Build(), Token);
        await _h.Request(first.Value.Launch);
        await _h.Fail(launch: first.Value.Launch);
        var third = await Service().Handle(A.OpenItemRequest().Build(), Token);   // the view now shows it failed: the note no longer holds the item

        Expect.Equal(SindriErrors.LaunchInProgress.Code, second.Error.Code);
        Assert.Contains(first.Value.Launch.Value, second.Error.Message, StringComparison.Ordinal);
        Assert.True(third.IsSuccess, third.IsError ? third.Error.ToString() : null);
    }

    [Fact]
    public async Task A_closed_item_is_SND_ITEM_409()
    {
        await Seed();
        await _h.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, ItemState.New, ItemState.Done, Version: 2, Reason: null), A.FirstItemId);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().WithVersion(2).Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_stale_item_version_is_SND_ITEM_409()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().WithVersion(7).Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Unknown_item_is_SND_ITEM_404()
    {
        _ = await _h.Slice.HoldLease();
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Without_the_lease_the_control_plane_is_not_running()
    {
        await _h.Item(_repo);
        using var entered = Human();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Fact]
    public async Task Without_a_caller_the_call_is_refused()
    {
        await Seed();

        var result = await Service().Handle(A.OpenItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Fact]
    public void The_validator_refuses_an_unknown_mode_and_a_version_below_one()
    {
        var validator = new OpenItemValidator();

        Assert.True(validator.Validate(A.OpenItemRequest().Build()).IsSuccess);
        Expect.Equal(SindriErrors.InvalidInput.Code, validator.Validate(A.OpenItemRequest().InMode((LaunchMode)9).Build()).Error.Code);
        Expect.Equal(SindriErrors.InvalidInput.Code, validator.Validate(A.OpenItemRequest().WithVersion(0).Build()).Error.Code);
    }
}
