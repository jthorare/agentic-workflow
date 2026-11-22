using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.MergeRun;

public sealed class MergeRunTests
{
    private readonly MetaWorld _world = new();
    private readonly InMemoryMetaEventStore _events = new();

    private MergeRunService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages), _world.Repo, _world.Gates, new MergeRecorder(_world.Repo, _world.Values, _world.Gates, _world.Log, Worlds.Reservation(_world), _world.Clock, _world.Board), new MergeEventWriter(new MetaEventSink(_events), _world.Messages, _world.Clock));

    private async Task<IReadOnlyList<(string Kind, string? Lane, string Fields)>> Written() =>
        [.. (await _events.Read(0, kind: null, lane: null, 100, CancellationToken.None)).Value.Events.Select(e => (e.Kind, e.Lane, e.Fields))];

    private async Task Ready(bool gated = true, bool mergeFails = false, bool fetchFails = false, string state = "OPEN", string? mergeCommit = null, string queue = QueueAnswers.Off, bool queueFails = false, bool positionFails = false, string? queueBefore = null, string? extensions = null, string stateAfterMerge = "MERGED")
    {
        await _world.ReadyWithGate();
        if (gated)
        {
            await SaveGate();
        }

        ScriptGitHub(mergeFails, fetchFails, state, mergeCommit, queue, queueFails, positionFails, queueBefore);
        ScriptStateAfterStackMerge(stateAfterMerge);
        if (extensions is not null)
        {
            ScriptStack(extensions);
        }
    }

    // The re-read after a stack merge: the pull request reads as `state` once "gh stack merge" ran, and OPEN before.
    private void ScriptStateAfterStackMerge(string state) =>
        _ = _world.Runner.When("gh pr view 190 --json state", _ =>
            new CommandResult(0, "{\"state\":\"" + (_world.Runner.Lines.Contains("gh stack merge 190 --yes", StringComparer.Ordinal) ? state : "OPEN") + "\"}", string.Empty, TimedOut: false));

    // What "gh extension list" prints: name, repo and version, tab separated.
    private const string StackInstalled = "gh stack\tgithub/gh-stack\tv1.0.0\n";

    private void ScriptStack(string extensions) =>
        _ = _world.Runner.When("gh extension list", extensions);

    private async Task AssignApiLane() =>
        _ = await _world.Lanes.Save(new LaneMapState(1, [A.Assigned("api", "t1-api")]), A.At(1), CancellationToken.None);

    private async Task SaveGate()
    {
        GateStep[] steps = [new("sync-rules", GateVerdict.Pass, "/l/sync.log")];
        _ = await _world.Gates.Save(new GateRecord(190, "headsha", "mainsha", ["repo"], steps, GateVerdict.Pass, A.At(5)), CancellationToken.None);
    }

    // The queue read before the merge (queueBefore, or queue when null), and the one after it for the position (which fails when positionFails).
    private void ScriptQueue(string queue, bool queueFails, bool positionFails, string? queueBefore)
    {
        var reads = 0;
        _ = _world.Runner.When("gh api graphql", _ =>
        {
            reads++;
            return queueFails || (positionFails && reads > 1)
                ? new CommandResult(1, string.Empty, "HTTP 502", TimedOut: false)
                : new CommandResult(0, reads == 1 ? queueBefore ?? queue : queue, string.Empty, TimedOut: false);
        });
    }

    private void ScriptGitHub(bool mergeFails, bool fetchFails, string state, string? mergeCommit, string queue, bool queueFails, bool positionFails, string? queueBefore)
    {
        // origin/main is mainsha until the merge, newmainsha1234 after it.
        var merged = false;
        ScriptQueue(queue, queueFails, positionFails, queueBefore);
        CommandResult Merge(CommandRequest request)
        {
            merged = !mergeFails;
            return new CommandResult(mergeFails ? 1 : 0, string.Empty, mergeFails ? "not mergeable" : string.Empty, TimedOut: false);
        }

        _ = _world.Runner
            .When("gh stack merge 190 --yes", Merge)
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"headsha","isDraft":false,"title":"Add x","state":"{state}"}""".Replace("{state}", state, StringComparison.Ordinal))
            .When("gh pr view 190 --json mergeCommit", mergeCommit is null ? string.Empty : "{\"mergeCommit\":{\"oid\":\"" + mergeCommit + "\"}}", mergeCommit is null ? 1 : 0)
            .When("gh pr merge 190 --merge", Merge)
            .When("rev-parse --verify --quiet origin/main", _ => new CommandResult(0, merged ? "newmainsha1234\n" : "mainsha\n", string.Empty, TimedOut: false))
            .When("git diff --name-only mainsha...headsha", "README.md\n")
            .When("git fetch", _ => new CommandResult(merged && fetchFails ? 1 : 0, string.Empty, merged && fetchFails ? "network down" : string.Empty, TimedOut: false));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_pull_request_is_merged_recorded_logged_and_announced()
    {
        await Ready();
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, "newmainsha1234", "main moved to newmain: #190 Add x merged. Merge main into your branches."), (result.Value.Merged, result.Value.Sha, result.Value.Announce));
        Assert.Equal(["gh pr merge 190 --merge"], _world.Runner.Lines.Where(l => l.StartsWith("gh pr merge", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Equal(new MergeRecord(190, "newmainsha1234", _world.Clock.GetUtcNow()), (await _world.Gates.LatestMerge(CancellationToken.None)).Value);
        Assert.Equal("- MERGE 2026-10-15 05:00: #190 Add x merged; main is now newmain", Assert.Single(_world.Log.Lines));
        Assert.DoesNotContain(await _world.Open(), o => o.Kind == ObligationKind.AnnounceMain);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_merge_appends_merge_recorded_with_the_lane_that_sent_the_ready_notice()
    {
        await Ready();
        await _world.Receive("m1", "t1-api", "ready #190 ab12cd3");

        _ = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.Equal([("merge.recorded", "api", """{"pr":190,"sha":"newmainsha1234","lane":"api"}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_merge_with_no_ready_notice_has_no_event_lane()
    {
        await Ready();

        _ = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.Equal([("merge.recorded", null, """{"pr":190,"sha":"newmainsha1234","lane":""}""")], await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_merge_appends_nothing()
    {
        await Ready(gated: false);

        _ = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.Empty(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_records_main_and_the_pull_request_on_the_board()
    {
        await Ready();
        await AssignApiLane();

        _ = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        var board = (await _world.Board.Read(CancellationToken.None)).Value;
        Assert.NotNull(board);
        Assert.Equal("newmainsha1234", board.MainSha);
        Assert.Equal([190], board.MergedPrs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_the_checks_refuse_is_not_merged()
    {
        await Ready(gated: false);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Merged);
        Assert.Contains(result.Value.Checks, c => c.Name == "gate" && !c.Ok);
        Assert.Null(result.Value.Announce);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh pr merge", StringComparison.Ordinal));
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
        Assert.Null((await _world.Board.Read(CancellationToken.None)).Value);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_gh_merge_is_an_error_and_records_nothing()
    {
        await Ready(mergeFails: true);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_is_recorded_even_when_the_fetch_after_it_fails_using_the_merge_commit_gh_reports()
    {
        await Ready(fetchFails: true, mergeCommit: "abcdef1234567");
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, "abcdef1234567"), (result.Value.Merged, result.Value.Sha));
        Assert.Equal("abcdef1234567", (await _world.Gates.LatestMerge(CancellationToken.None)).Value!.Sha);
        Assert.Equal("abcdef1234567", (await _world.Board.Read(CancellationToken.None)).Value!.MainSha);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_whose_sha_cannot_be_read_is_recorded_with_the_sha_unknown_and_announced()
    {
        await Ready(fetchFails: true);
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, null, "main moved (#190 Add x merged). Merge main into your branches."), (result.Value.Merged, result.Value.Sha, result.Value.Announce));
        Assert.Equal(MergeRecord.Unknown, (await _world.Gates.LatestMerge(CancellationToken.None)).Value!.Sha);
        Assert.DoesNotContain(await _world.Open(), o => o.Kind == ObligationKind.AnnounceMain);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Running_merge_again_on_a_merged_pull_request_with_no_merge_record_records_it()
    {
        await Ready(state: "MERGED", mergeCommit: "abcdef1234567");
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.True(result.Value.Merged);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh pr merge", StringComparison.Ordinal));
        Assert.Equal("abcdef1234567", (await _world.Gates.LatestMerge(CancellationToken.None)).Value!.Sha);
        Assert.Equal("abcdef1234567", (await _world.Board.Read(CancellationToken.None)).Value!.MainSha);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merged_pull_request_that_already_has_a_merge_record_is_not_recorded_twice()
    {
        await Ready(state: "MERGED");
        _ = await _world.Gates.RecordMerge(new MergeRecord(190, "oldsha1234", A.At(2)), CancellationToken.None);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.False(result.Value.Merged);
        Assert.Empty(_world.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_pull_request_number_must_be_positive() =>
        Assert.Equal(MetaErrors.Invalid.Code, new MergeRunValidator().Validate(new MergeRunRequest(-1)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public async Task With_a_merge_queue_a_ready_pull_request_is_queued_with_its_position_and_no_merge_is_recorded()
    {
        await Ready(queue: QueueAnswers.Queued(3), queueBefore: QueueAnswers.OnNotQueued);
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((false, true, 3, null, null), (result.Value.Merged, result.Value.Queued, result.Value.Position, result.Value.Sha, result.Value.Announce));
        Assert.Equal(["gh pr merge 190 --merge"], _world.Runner.Lines.Where(l => l.StartsWith("gh pr merge", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
        Assert.Null((await _world.Board.Read(CancellationToken.None)).Value);
        Assert.DoesNotContain(await _world.Open(), o => o.Kind == ObligationKind.AnnounceMain);
        var queued = Assert.Single(MergeQueued.Parse((await _world.Values.Get(KvKeys.MergeQueued, CancellationToken.None)).Value).Values);
        Assert.Equal(new QueuedMerge(190, _world.Clock.GetUtcNow(), "headsha"), queued);
        Assert.Contains(_world.Log.Lines, l => string.Equals(l, "- NOTE 2026-10-15 05:00: #190 Add x queued in GitHub's merge queue (position 3)", StringComparison.Ordinal));
        Assert.DoesNotContain(_world.Log.Lines, l => l.StartsWith("- MERGE", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_a_merge_queue_an_unreadable_position_is_left_out_and_the_pull_request_is_still_queued()
    {
        // The first read (before the merge) works; the second one, for the position, fails.
        await Ready(queue: QueueAnswers.Queued(1), positionFails: true, queueBefore: QueueAnswers.OnNotQueued);
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, null), (result.Value.Queued, result.Value.Position));
        Assert.Contains(_world.Log.Lines, l => string.Equals(l, "- NOTE 2026-10-15 05:00: #190 Add x queued in GitHub's merge queue", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_queue_read_refuses_before_gh_merges_anything()
    {
        await Ready(queueFails: true);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh pr merge", StringComparison.Ordinal));
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_merge_queue_the_merge_is_recorded_at_once_and_nothing_is_kept_as_queued()
    {
        await Ready();
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, false), (result.Value.Merged, result.Value.Queued));
        Assert.Null((await _world.Values.Get(KvKeys.MergeQueued, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_already_in_githubs_merge_queue_is_not_merged_again()
    {
        await Ready(queue: QueueAnswers.Queued(2));
        await AssignApiLane();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal((true, 2), (result.Value.Queued, result.Value.Position));
        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh pr merge", StringComparison.Ordinal));
        Assert.Equal([190], MergeQueued.Parse((await _world.Values.Get(KvKeys.MergeQueued, CancellationToken.None)).Value).Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merge_run_uses_gh_stack_merge_for_a_stacked_pr()
    {
        await Ready(queue: QueueAnswers.Stacked(), extensions: StackInstalled);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.True(result.Value.Merged);
        Assert.Equal(["gh stack merge 190 --yes"], _world.Runner.Lines.Where(l => l.Contains(" merge ", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Equal(190, (await _world.Gates.LatestMerge(CancellationToken.None)).Value?.Pr);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merge_run_refuses_a_stacked_pr_when_gh_stack_is_missing()
    {
        await Ready(queue: QueueAnswers.Stacked(), extensions: "gh copilot\tgithub/gh-copilot\tv1.0.0\n");

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.StackExtensionMissing.Code, result.Error.Code);
        Assert.Contains("gh extension install github/gh-stack", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains(" merge ", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stacked_pr_in_a_merge_queue_base_is_enqueued_by_gh_stack_merge()
    {
        await Ready(queue: QueueAnswers.Stacked(queue: true), extensions: StackInstalled);

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Contains("gh stack merge 190 --yes", _world.Runner.Lines, StringComparer.Ordinal);
        Assert.DoesNotContain("gh pr merge 190 --merge", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unstacked_pr_never_asks_which_extensions_are_installed()
    {
        await Ready(extensions: StackInstalled);

        _ = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh extension", StringComparison.Ordinal));
        Assert.Contains("gh pr merge 190 --merge", _world.Runner.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_host_without_the_stack_field_is_asked_again_and_reads_as_not_stacked()
    {
        var asked = new List<string>();
        _ = _world.Runner.When("gh api graphql", request =>
        {
            var query = request.Argv[^1];
            asked.Add(query);
            return query.Contains("stack{number}", StringComparison.Ordinal)
                ? new CommandResult(1, string.Empty, "gh: Field 'stack' doesn't exist on type 'PullRequest'", TimedOut: false)
                : new CommandResult(0, QueueAnswers.OnNotQueued, string.Empty, TimedOut: false);
        });

        var view = await _world.Repo.QueueView("/repo", 190);

        Assert.True(view.IsSuccess, view.IsError ? view.Error.Message : string.Empty);
        Assert.Equal((true, false), (view.Value.Enabled, view.Value.Stacked));
        Assert.Equal(2, asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stack_merge_that_exits_0_is_not_recorded_while_the_pr_is_still_open()
    {
        await Ready(queue: QueueAnswers.Stacked(), extensions: StackInstalled, stateAfterMerge: "OPEN");

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.CommandFailed.Code, result.Error.Code);
        Assert.Contains("still open", result.Error.Message, StringComparison.Ordinal);
        Assert.Null((await _world.Gates.LatestMerge(CancellationToken.None)).Value);
        Assert.Empty(await Written());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merge_order_refuses_a_stacked_pr_while_a_lower_pr_of_the_stack_is_unmerged()
    {
        await Ready(queue: QueueAnswers.Stacked(), extensions: StackInstalled);
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        _ = await _world.Config.Save(team with { MergeOrder = [new MergeOrderRule(189, 190)] }, CancellationToken.None);   // 189 is the lower PR of the stack
        _ = _world.Runner.When("gh pr view 189 --json state", """{"state":"OPEN"}""");

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.False(result.Value.Merged);
        Assert.Contains(result.Value.Checks, c => c.Name.Equals("order", StringComparison.Ordinal) && !c.Ok);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains(" merge ", StringComparison.Ordinal));
    }
}
