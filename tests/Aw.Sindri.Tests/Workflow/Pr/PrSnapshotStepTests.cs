using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>'step.pr.snapshot' and 'step.pr.intake' (babysitter spec §4.2, §4.3): one forge read, the first outcome that holds, and the fact the queue, ready-check and merge gate read.</summary>
[Lane(TestLane.Unit)]
public sealed class PrSnapshotStepTests
{
    private const string Host = "forge.test";
    private const string Head = "aaaa1111";
    private const string Address = "T-3/pr-babysitter/1";

    private readonly FakePullRequests _pulls = new([Host]);
    private readonly FakePull _pull;
    private readonly InMemoryPrCommentMarks _marks = new();
    private readonly FakeRunDiff _diff = new();
    private readonly InMemoryRunArea _area = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-10-22T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    public PrSnapshotStepTests()
    {
        _pull = _pulls.Add(new PullRepo(Host, "acme", "app"), "feat/x", Head);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PullThread Thread(string key, string author = "alice", bool resolved = false, IReadOnlyList<string>? markers = null) =>
        new(key, PullCommentKind.Thread, author, "MEMBER", IsBot: false, resolved, Outdated: false, "a.cs", $"fix {key}", markers ?? [], new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), Line: null, EditedAt: null, Url: null);

    private Task<BlockStepResult> Snapshot(string? pr = null, Dictionary<string, string>? more = null) => Run(new PrSnapshotStep(_pulls, _marks, _diff, _clock, new MergeRecord(_area, _clock)), "step.pr.snapshot", pr, more: more);

    private Task<BlockStepResult> Intake(string? pr = null, string? workspace = null, Dictionary<string, string>? more = null) => Run(new PrIntakeStep(_pulls, new MergeRecord(_area, _clock)), "step.pr.intake", pr, workspace, more);

    private async Task<BlockStepResult> Run(IBlockStep step, string id, string? pr, string? workspace = null, Dictionary<string, string>? more = null)
    {
        var facts = new Dictionary<string, string>(more ?? [], StringComparer.Ordinal);
        if ((pr ?? _pull.Link) is { Length: > 0 } link && pr != "")
        {
            facts["pr"] = link;
        }

        if (workspace is not null)
        {
            facts["workspace"] = workspace;
        }

        return await step.Run(BuildShipContexts.Of(id, facts, address: Address), Token);
    }

    [Fact]
    public async Task The_first_snapshot_appends_a_snapshot_row_with_the_outcome_the_checks_and_the_queue_counts()
    {
        _pull.Threads.Add(Thread("t1"));

        _ = await Snapshot();

        var row = Single(await Rows());
        Assert.Equal("snapshot", row.GetProperty("kind").GetString());
        Assert.Equal(Head, row.GetProperty("headSha").GetString());
        Assert.Equal(Address, row.GetProperty("instance").GetString());
        Assert.Equal("comments", row.GetProperty("outcome").GetString());
        Assert.Equal(Head, row.GetProperty("checks").GetProperty("sha").GetString());
        Assert.Equal(1, row.GetProperty("queue").GetProperty("unhandled").GetInt32());
    }

    [Fact]
    public async Task A_snapshot_with_the_same_outcome_and_head_as_the_last_appends_no_row()
    {
        var first = await Snapshot();

        _ = await Snapshot(more: Previous(first));

        _ = Single(await Rows());
    }

    [Fact]
    public async Task A_moved_head_or_a_new_outcome_appends_another_row()
    {
        var first = await Snapshot();
        _pull.HeadSha = "bbbb2222";
        var second = await Snapshot(more: Previous(first));

        _ = await Snapshot(more: Previous(second));

        Assert.Equal(2, (await Rows()).Length);
    }

    [Fact]
    public async Task A_retried_snapshot_appends_its_row_once()
    {
        var first = await Snapshot();
        _pull.HeadSha = "bbbb2222";
        var previous = Previous(first);

        _ = await Snapshot(more: previous);
        _ = await Snapshot(more: previous);

        Assert.Equal(2, (await Rows()).Length);
    }

    [Fact]
    public async Task A_step_with_no_instance_address_still_snapshots_and_appends_nothing()
    {
        var result = await new PrSnapshotStep(_pulls, _marks, _diff, _clock, new MergeRecord(_area, _clock)).Run(BuildShipContexts.Of("step.pr.snapshot", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = _pull.Link }), Token);

        Assert.NotNull(result.Result);
        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task Intake_appends_a_start_row_with_the_pull_request_the_handoff_hash_the_phase_and_the_line()
    {
        _ = await Intake(more: new(StringComparer.Ordinal) { ["handoff"] = "the hand-off text", ["phasePacket"] = """{"id":"P1"}""", ["lineContext"] = """{"runArea":"line/run"}""" });

        var row = Single(await Rows());
        Assert.Equal("start", row.GetProperty("kind").GetString());
        Assert.Equal(Head, row.GetProperty("headSha").GetString());
        Assert.Equal(_pull.Link, row.GetProperty("pr").GetString());
        Assert.StartsWith("sha256:", row.GetProperty("handoffHash").GetString(), StringComparison.Ordinal);
        Assert.Equal("P1", row.GetProperty("phase").GetString());
        Assert.Equal("line", row.GetProperty("startedBy").GetString());
    }

    [Fact]
    public async Task Intake_of_a_hand_started_pull_request_records_no_handoff_and_no_phase()
    {
        _ = await Intake();

        var row = Single(await Rows());
        Assert.Equal("hand", row.GetProperty("startedBy").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("handoffHash").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("phase").ValueKind);
    }

    [Fact]
    public async Task A_retried_intake_appends_its_start_row_once()
    {
        _ = await Intake();
        _ = await Intake();

        _ = Single(await Rows());
    }

    [Fact]
    public async Task Intake_of_a_pull_request_it_cannot_read_appends_nothing()
    {
        _ = await Intake(pr: "https://forge.test/acme/app/pull/99");

        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task The_first_snapshot_records_the_base_branch_and_its_tip()
    {
        _pull.Base = "feat/a";
        _pull.BaseSha = "tip-a1";

        var state = Fact(await Snapshot(), "fact:prState").GetProperty("base");

        Assert.Equal(("feat/a", "tip-a1", "tip-a1"), (state.GetProperty("ref").GetString(), state.GetProperty("sha").GetString(), state.GetProperty("tip").GetString()));
    }

    [Fact]
    public async Task A_base_that_moved_keeps_the_tip_the_pull_request_was_built_on_and_shows_the_new_one()
    {
        _pull.Base = "feat/a";
        _pull.BaseSha = "tip-a1";
        var first = await Snapshot();
        _pull.BaseSha = "tip-a2";

        var state = Fact(await Snapshot(more: Previous(first)), "fact:prState").GetProperty("base");

        Assert.Equal(("feat/a", "tip-a1", "tip-a2"), (state.GetProperty("ref").GetString(), state.GetProperty("sha").GetString(), state.GetProperty("tip").GetString()));
    }

    [Fact]
    public async Task A_base_that_was_squash_merged_and_deleted_keeps_its_last_tip_and_names_the_branch_it_was()
    {
        _pull.Base = "feat/a";
        _pull.BaseSha = "tip-a1";
        var first = await Snapshot();
        _pull.Base = "main";   // the base pull request merged, its branch is gone and the forge moved this one to main
        _pull.BaseSha = "tip-main";

        var state = Fact(await Snapshot(more: Previous(first)), "fact:prState").GetProperty("base");

        Assert.Equal(("main", "tip-a1", "tip-main", "feat/a"), (state.GetProperty("ref").GetString(), state.GetProperty("sha").GetString(), state.GetProperty("tip").GetString(), state.GetProperty("was").GetString()));
    }

    [Fact]
    public async Task The_old_tip_survives_further_snapshots_until_a_restack_settles()
    {
        _pull.Base = "feat/a";
        _pull.BaseSha = "tip-a1";
        var first = await Snapshot();
        _pull.Base = "main";
        _pull.BaseSha = "tip-main";
        var second = await Snapshot(more: Previous(first));

        var third = Fact(await Snapshot(more: Previous(second)), "fact:prState").GetProperty("base");

        Assert.Equal(("tip-a1", "feat/a"), (third.GetProperty("sha").GetString(), third.GetProperty("was").GetString()));
    }

    [Fact]
    public async Task A_restack_that_settled_at_this_head_makes_the_current_tip_the_one_the_pull_request_sits_on()
    {
        _pull.Base = "feat/a";
        _pull.BaseSha = "tip-a1";
        var first = await Snapshot();
        _pull.Base = "main";
        _pull.BaseSha = "tip-main";
        var more = Previous(first);
        more["restack"] = $$$"""{"outcome":"restacked","head":"{{{Head}}}","oldBase":"feat/a","newBase":"main"}""";

        var state = Fact(await Snapshot(more: more), "fact:prState").GetProperty("base");

        Assert.Equal(("tip-main", "tip-main"), (state.GetProperty("sha").GetString(), state.GetProperty("tip").GetString()));
        Assert.False(state.TryGetProperty("was", out var was) && was.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task A_restack_of_another_head_does_not_settle_this_one()
    {
        _pull.Base = "feat/a";
        _pull.BaseSha = "tip-a1";
        var first = await Snapshot();
        _pull.BaseSha = "tip-a2";
        var more = Previous(first);
        more["restack"] = """{"outcome":"restacked","head":"some-other-head","oldBase":"feat/a","newBase":"feat/a"}""";

        var state = Fact(await Snapshot(more: more), "fact:prState").GetProperty("base");

        Assert.Equal("tip-a1", state.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task A_forge_that_gives_no_base_tip_writes_the_branch_and_no_sha()
    {
        _pull.Base = "feat/a";

        var state = Fact(await Snapshot(), "fact:prState").GetProperty("base");

        Assert.Equal("feat/a", state.GetProperty("ref").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("sha").ValueKind);
    }

    private static Dictionary<string, string> Previous(BlockStepResult result) => new(StringComparer.Ordinal) { ["prState"] = Fact(result, "fact:prState").GetRawText() };

    private async Task<string[]> Rows() => (await _area.Read(Address, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static JsonElement Single(string[] lines) => JsonDocument.Parse(Assert.Single(lines)).RootElement.Clone();

    private static JsonElement Fact(BlockStepResult result, string key) => result.Result!.Outputs.Single(o => string.Equals(o.Key.Value, key, StringComparison.Ordinal)).Value;

    private static string Outcome(BlockStepResult result) => result.Result!.Outcome.Value;

    private ValueTask<Result<bool>> Mark(string comment, string outcome, int minutes, string effect, string? reason = null, string? commit = null) =>
        _marks.Write(new PrCommentMark(new PrRef(Host, "acme/app", _pull.Number), PrCommentId.From(comment), EditedAt: null, InstanceAddress.From("T-3/item/1"), outcome, reason, commit, new DateTimeOffset(2026, 10, 22, 7, minutes, 0, TimeSpan.Zero), effect), Token);

    private static JsonElement CommentOf(JsonElement state, string key) => state.GetProperty("comments").EnumerateArray().Single(c => string.Equals(c.GetProperty("key").GetString(), key, StringComparison.Ordinal));

    [Fact]
    public async Task A_comment_carries_its_latest_mark()
    {
        _pull.Threads.Add(Thread("t1"));
        _pull.Threads.Add(Thread("t2"));
        _ = await Mark("t1", "thread", minutes: 1, effect: "e1");
        _ = await Mark("t1", "fixed", minutes: 5, effect: "e2", commit: "bbbb2222");

        var state = Fact(await Snapshot(), "fact:prState");

        var mark = CommentOf(state, "t1").GetProperty("mark");
        Assert.Equal("fixed", mark.GetProperty("outcome").GetString());
        Assert.Equal("bbbb2222", mark.GetProperty("commit").GetString());
        Assert.Equal(JsonValueKind.Null, mark.GetProperty("reason").ValueKind);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T07:05:00Z", System.Globalization.CultureInfo.InvariantCulture), mark.GetProperty("at").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, CommentOf(state, "t2").GetProperty("mark").ValueKind);
    }

    [Fact]
    public async Task The_held_count_is_the_comments_whose_latest_mark_is_hold()
    {
        _pull.Threads.Add(Thread("t1"));
        _pull.Threads.Add(Thread("t2"));
        _pull.Threads.Add(Thread("t3"));
        _ = await Mark("t1", "held", minutes: 1, effect: "e1", reason: "type");
        _ = await Mark("t2", "held", minutes: 1, effect: "e2", reason: "type");
        _ = await Mark("t2", "fixed", minutes: 2, effect: "e3");

        var state = Fact(await Snapshot(), "fact:prState");

        Assert.Equal(1, state.GetProperty("queue").GetProperty("held").GetInt32());
        Assert.Equal("type", CommentOf(state, "t1").GetProperty("mark").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Checks_on_another_commit_than_the_head_make_the_snapshot_stale()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.ChecksSha = "old0000";

        Assert.True(Fact(await Snapshot(), "fact:prState").GetProperty("stale").GetBoolean());

        _pull.ChecksSha = null;

        Assert.False(Fact(await Snapshot(), "fact:prState").GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task Marked_comments_are_handled_and_leave_the_unhandled_count()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.Threads.Add(Thread("t1"));
        _pull.Threads.Add(Thread("t2"));
        _pull.Threads.Add(Thread("t3"));
        _ = await Mark("t1", "held", minutes: 1, effect: "e1", reason: "type");
        _ = await Mark("t2", "fixed", minutes: 1, effect: "e2", commit: "bbbb2222");

        var state = Fact(await Snapshot(), "fact:prState");

        Assert.Equal(1, state.GetProperty("queue").GetProperty("unhandled").GetInt32());
        Assert.Equal(1, state.GetProperty("queue").GetProperty("held").GetInt32());
    }

    [Fact]
    public async Task Comments_that_are_all_marked_do_not_trigger_the_comments_outcome()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.Threads.Add(Thread("t1"));
        _pull.Threads.Add(Thread("t2"));
        _ = await Mark("t1", "held", minutes: 1, effect: "e1", reason: "type");
        _ = await Mark("t2", "fixed", minutes: 1, effect: "e2", commit: "bbbb2222");

        Assert.Equal("green", Outcome(await Snapshot()));
    }

    [Fact]
    public async Task A_merged_pull_request_reports_merged_and_its_sha()
    {
        _pull.State = PullState.Merged;

        var result = await Snapshot();

        Assert.Equal("merged", Outcome(result));
        Assert.Equal(Head, Fact(result, "fact:mergedSha").GetString());
        Assert.Equal("merged", Fact(result, "fact:prState").GetProperty("pullState").GetString());
    }

    [Fact]
    public async Task A_merged_pull_request_the_instance_merged_reports_the_merge_commit_not_the_head()
    {
        _pull.State = PullState.Merged;

        var result = await Snapshot(more: new(StringComparer.Ordinal) { ["merge"] = """{ "mergedSha": "mmmm9999", "method": "squash", "headSha": "aaaa1111" }""" });

        Assert.Equal("mmmm9999", Fact(result, "fact:mergedSha").GetString());
    }

    [Fact]
    public async Task A_closed_pull_request_reports_closed()
    {
        _pull.State = PullState.Closed;

        Assert.Equal("closed", Outcome(await Snapshot()));
    }

    [Fact]
    public async Task A_conflict_is_reported_before_the_checks()
    {
        _pull.Mergeable = false;
        _pull.ChecksState = PullChecksState.Failing;
        _pull.FailingChecks.Add("build");

        Assert.Equal("conflict", Outcome(await Snapshot()));
    }

    [Fact]
    public async Task A_failing_check_on_the_head_is_ciRed_and_comes_before_comments()
    {
        _pull.ChecksState = PullChecksState.Failing;
        _pull.FailingChecks.Add("build");
        _pull.Threads.Add(Thread("thread:1"));

        var result = await Snapshot();

        Assert.Equal("ciRed", Outcome(result));
        Assert.Equal("build", Fact(result, "fact:prState").GetProperty("checks").GetProperty("failing")[0].GetString());
    }

    [Fact]
    public async Task Checks_that_ran_on_an_older_commit_are_pending()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.ChecksSha = "old0000";

        Assert.Equal("pending", Outcome(await Snapshot()));
    }

    [Fact]
    public async Task Running_checks_are_pending()
    {
        _pull.ChecksState = PullChecksState.Pending;

        Assert.Equal("pending", Outcome(await Snapshot()));
    }

    [Fact]
    public async Task An_unresolved_comment_reports_comments_and_the_queue_lists_threads_oldest_first_in_camelCase()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.Threads.Add(Thread("thread:1"));
        _pull.Threads.Add(Thread("thread:2", resolved: true));
        _pull.Threads.Add(Thread("thread:3"));

        var result = await Snapshot();

        var state = Fact(result, "fact:prState");
        Assert.Equal("comments", Outcome(result));
        Assert.Equal(["thread:1", "thread:2", "thread:3"], state.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("key").GetString()), StringComparer.Ordinal);
        Assert.Equal("alice", state.GetProperty("comments")[0].GetProperty("author").GetString());
        Assert.Equal(2, state.GetProperty("queue").GetProperty("unhandled").GetInt32());
    }

    [Fact]
    public async Task Sindris_marked_replies_and_resolved_threads_are_not_work()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.Threads.Add(Thread("thread:1", markers: ["sindri:reply:0"]));
        _pull.Threads.Add(Thread("thread:2", markers: ["sindri:reply:1"]));
        _pull.Threads.Add(Thread("thread:3", resolved: true));

        var result = await Snapshot();

        Assert.Equal("green", Outcome(result));
        Assert.Equal(JsonValueKind.Null, Fact(result, "fact:prState").GetProperty("self").ValueKind);
    }

    [Fact]
    public async Task Green_checks_on_the_head_with_an_approval_report_green_and_the_merge_is_not_blocked()
    {
        _pull.ChecksState = PullChecksState.Passing;
        _pull.ReviewDecision = "APPROVED";

        var result = await Snapshot();

        var state = Fact(result, "fact:prState");
        Assert.Equal("green", Outcome(result));
        Assert.Equal("green", state.GetProperty("state").GetString());
        Assert.Equal(0, state.GetProperty("mergeBlockedBy").GetArrayLength());
        Assert.Equal(Head, state.GetProperty("headSha").GetString());
        Assert.Equal("passing", state.GetProperty("checks").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Green_without_an_approving_review_is_blocked_by_reviewRequired()
    {
        _pull.ChecksState = PullChecksState.Passing;

        var result = await Snapshot();

        Assert.Equal("green", Outcome(result));
        Assert.Equal(["reviewRequired"], Fact(result, "fact:prState").GetProperty("mergeBlockedBy").EnumerateArray().Select(b => b.GetString()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Every_outcome_writes_the_snapshot_with_the_pull_request_fields_and_the_time()
    {
        _pull.Mergeable = null;
        _pull.ChecksState = PullChecksState.Pending;

        var state = Fact(await Snapshot(), "fact:prState");

        Assert.Equal(_pull.Link, state.GetProperty("link").GetString());
        Assert.Equal("open", state.GetProperty("pullState").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("mergeable").ValueKind);
        Assert.Equal(0, state.GetProperty("unresolvedThreads").GetInt32());
        Assert.StartsWith("2026-10-22T08:00:00", state.GetProperty("at").GetString(), StringComparison.Ordinal);
    }

    private static JsonElement Status(BlockStepResult result) => Fact(result, "fact:prStatus");

    [Fact]
    public async Task Every_outcome_writes_prStatus_with_the_exact_shape_the_run_view_reads()
    {
        _pull.ChecksState = PullChecksState.Passing;

        var status = Status(await Snapshot());

        Assert.Equal(["pr", "url", "checks", "review", "proof", "docs"], status.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(_pull.Number, status.GetProperty("pr").GetInt32());
        Assert.Equal(_pull.Link, status.GetProperty("url").GetString());
        Assert.Equal(["passed", "failed", "pending"], status.GetProperty("checks").EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(["open", "resolved"], status.GetProperty("review").EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_failed_check_count_is_the_failing_checks()
    {
        _pull.ChecksState = PullChecksState.Failing;
        _pull.FailingChecks.Add("build");
        _pull.FailingChecks.Add("lint");

        var checks = Status(await Snapshot()).GetProperty("checks");

        Assert.Equal(2, checks.GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task The_passed_and_pending_counts_are_the_forges()
    {
        _pull.Checks = new PullChecks(PullChecksState.Pending, Head, [], Passed: 7, Pending: 3);

        var checks = Status(await Snapshot()).GetProperty("checks");

        Assert.Equal(7, checks.GetProperty("passed").GetInt32());
        Assert.Equal(3, checks.GetProperty("pending").GetInt32());
        Assert.Equal(0, checks.GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task The_review_counts_are_the_unresolved_threads_that_are_not_sindris_and_the_resolved_ones()
    {
        _pull.Threads.Add(Thread("t1"));
        _pull.Threads.Add(Thread("t2", resolved: true));
        _pull.Threads.Add(Thread("t3", resolved: true));
        _pull.Threads.Add(Thread("t4", markers: ["sindri:reply"]));

        var review = Status(await Snapshot()).GetProperty("review");

        Assert.Equal(1, review.GetProperty("open").GetInt32());
        Assert.Equal(2, review.GetProperty("resolved").GetInt32());
    }

    [Fact]
    public async Task The_proof_is_null_when_the_instance_holds_no_recheck() =>
        Assert.Equal(JsonValueKind.Null, Status(await Snapshot()).GetProperty("proof").ValueKind);

    [Theory]
    [InlineData("held", "passed")]
    [InlineData("broken", "failed")]
    [InlineData("nothing", "none")]
    public async Task The_proof_follows_the_recheck_verdict_on_the_pull_requests_head(string verdict, string expected)
    {
        var recheck = $$"""{ "headSha": "{{Head}}", "verdict": "{{verdict}}" }""";

        var proof = Status(await Snapshot(more: new(StringComparer.Ordinal) { ["recheck"] = recheck })).GetProperty("proof");

        Assert.Equal(expected, proof.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_recheck_of_another_commit_says_nothing_of_the_head()
    {
        var recheck = """{ "headSha": "old00000", "verdict": "held" }""";

        var proof = Status(await Snapshot(more: new(StringComparer.Ordinal) { ["recheck"] = recheck })).GetProperty("proof");

        Assert.Equal(JsonValueKind.Null, proof.ValueKind);
    }

    [Fact]
    public async Task The_docs_are_missing_when_no_docs_sync_has_ended() =>
        Assert.Equal("missing", Status(await Snapshot()).GetProperty("docs").GetProperty("status").GetString());

    [Theory]
    [InlineData("none")]
    [InlineData("disabled")]
    [InlineData("skippedByOwner")]
    public async Task The_docs_are_notNeeded_when_docs_sync_found_nothing_or_the_owner_skipped_them(string result)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["docsResult"] = $$"""{ "result": "{{result}}" }""" };

        var docs = Status(await Snapshot(more: facts)).GetProperty("docs");

        Assert.Equal("notNeeded", docs.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_docs_are_updated_when_the_docs_pull_request_is_green()
    {
        var docs = DocsPull();
        _pull.ChecksState = PullChecksState.Passing;

        var status = Status(await Snapshot(more: DocsFacts(docs))).GetProperty("docs");

        Assert.Equal("updated", status.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_docs_are_updated_when_the_docs_pull_request_is_merged()
    {
        var docs = DocsPull();
        docs.State = PullState.Merged;

        var status = Status(await Snapshot(more: DocsFacts(docs))).GetProperty("docs");

        Assert.Equal("updated", status.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_docs_are_missing_while_the_docs_pull_request_is_red()
    {
        var docs = DocsPull();
        docs.ChecksState = PullChecksState.Failing;

        var status = Status(await Snapshot(more: DocsFacts(docs))).GetProperty("docs");

        Assert.Equal("missing", status.GetProperty("status").GetString());
    }

    private FakePull DocsPull(string head = "dddd4444")
    {
        var docs = _pulls.Add(new PullRepo(Host, "acme", "app"), "docs/feat/x", head);
        docs.ChecksState = PullChecksState.Passing;
        return docs;
    }

    private static Dictionary<string, string> DocsFacts(FakePull docs, string? result = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["docsPr"] = docs.Link };
        if (result is not null)
        {
            facts["docsResult"] = $$"""{ "result": "{{result}}" }""";
        }

        return facts;
    }

    [Fact]
    public async Task A_merged_pull_request_whose_docs_pull_request_is_open_reports_mergedDocsOpen_and_its_sha()
    {
        _pull.State = PullState.Merged;
        var docs = DocsPull();

        var result = await Snapshot(more: DocsFacts(docs));

        Assert.Equal("mergedDocsOpen", Outcome(result));
        Assert.Equal(Head, Fact(result, "fact:mergedSha").GetString());
    }

    [Fact]
    public async Task A_merged_pull_request_whose_docs_pull_request_is_merged_reports_merged()
    {
        _pull.State = PullState.Merged;
        var docs = DocsPull();
        docs.State = PullState.Merged;

        Assert.Equal("merged", Outcome(await Snapshot(more: DocsFacts(docs))));
    }

    [Fact]
    public async Task A_red_docs_pull_request_reports_docsRed_after_the_comments_and_before_pending()
    {
        _pull.ChecksState = PullChecksState.Pending;
        var docs = DocsPull();
        docs.ChecksState = PullChecksState.Failing;
        docs.FailingChecks.Add("docs-build");

        var red = await Snapshot(more: DocsFacts(docs));
        _pull.Threads.Add(Thread("t1"));
        var withComments = await Snapshot(more: DocsFacts(docs));

        Assert.Equal(("docsRed", "comments"), (Outcome(red), Outcome(withComments)));
        Assert.Equal("docs-build", Fact(red, "fact:prState").GetProperty("docs").GetProperty("checks").GetProperty("failing")[0].GetString());
    }

    [Fact]
    public async Task A_red_docs_pull_request_the_owner_skipped_is_not_docsRed()
    {
        _pull.ChecksState = PullChecksState.Passing;
        var docs = DocsPull();
        docs.ChecksState = PullChecksState.Failing;

        Assert.Equal("green", Outcome(await Snapshot(more: DocsFacts(docs, result: "skippedByOwner"))));
    }

    [Fact]
    public async Task A_docs_pull_request_with_checks_running_keeps_the_snapshot_pending()
    {
        _pull.ChecksState = PullChecksState.Passing;
        var docs = DocsPull();
        docs.ChecksState = PullChecksState.Pending;

        Assert.Equal("pending", Outcome(await Snapshot(more: DocsFacts(docs))));
    }

    [Fact]
    public async Task A_workspace_on_another_commit_than_the_pull_request_head_is_ahead()
    {
        _diff.Add("/work/tree", "bbbb2222");
        _diff.Add("/work/same", Head);

        var ahead = Fact(await Snapshot(more: new(StringComparer.Ordinal) { ["workspace"] = "/work/tree" }), "fact:prState");
        var same = Fact(await Snapshot(more: new(StringComparer.Ordinal) { ["workspace"] = "/work/same" }), "fact:prState");
        var none = Fact(await Snapshot(), "fact:prState");

        Assert.Equal((true, false, false), (ahead.GetProperty("workspaceAhead").GetBoolean(), same.GetProperty("workspaceAhead").GetBoolean(), none.GetProperty("workspaceAhead").GetBoolean()));
    }

    [Fact]
    public async Task A_snapshot_without_a_docs_pull_request_carries_no_docs()
    {
        var state = Fact(await Snapshot(), "fact:prState");

        Assert.Equal(JsonValueKind.Null, state.GetProperty("docs").ValueKind);
    }

    [Fact]
    public async Task The_snapshot_carries_the_link_as_url_and_the_head_sha_for_the_restack_effect()
    {
        var state = Fact(await Snapshot(), "fact:prState");

        Assert.Equal((_pull.Link, Head), (state.GetProperty("url").GetString(), state.GetProperty("head").GetProperty("sha").GetString()));
    }

    [Fact]
    public async Task A_missing_link_or_an_unreadable_pull_request_fails_the_Step()
    {
        var missing = await Snapshot(pr: "");
        var gone = await Snapshot(pr: "https://forge.test/acme/app/pull/99");

        Assert.Null(missing.Result);
        Assert.Null(gone.Result);
    }

    [Fact]
    public async Task Intake_seeds_the_facts_every_later_Step_reads_and_keeps_the_given_workspace()
    {
        var result = await Intake(workspace: "/work/tree");

        Assert.Equal("seeded", Outcome(result));
        Assert.Equal("/work/tree", Fact(result, "fact:workspace").GetString());
        Assert.Equal(
            ["fact:decision", "fact:fix", "fact:handoffNote", "fact:ownerNote", "fact:recheck", "fact:reply", "fact:workspace"],
            result.Result!.Outputs.Select(o => o.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Intake_fails_on_a_pull_request_it_cannot_read()
    {
        var result = await Intake(pr: "https://forge.test/acme/app/pull/99");

        Assert.Null(result.Result);
    }
}
