using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>'step.pr.snapshot' and 'step.pr.intake' (babysitter spec §4.2, §4.3): one forge read, the first outcome that holds, and the fact the queue, ready-check and merge gate read.</summary>
[Lane(TestLane.Unit)]
public sealed class PrSnapshotStepTests
{
    private const string Host = "forge.test";
    private const string Head = "aaaa1111";

    private readonly FakePullRequests _pulls = new([Host]);
    private readonly FakePull _pull;

    public PrSnapshotStepTests()
    {
        _pull = _pulls.Add(new PullRepo(Host, "acme", "app"), "feat/x", Head);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PullThread Thread(string key, string author = "alice", bool resolved = false, IReadOnlyList<string>? markers = null) =>
        new(key, PullCommentKind.Thread, author, "MEMBER", IsBot: false, resolved, Outdated: false, "a.cs", $"fix {key}", markers ?? [], DateTimeOffset.UnixEpoch, Line: null, EditedAt: null, Url: null);

    private Task<BlockStepResult> Snapshot(string? pr = null) => Run(new PrSnapshotStep(_pulls, new FakeTimeProvider(DateTimeOffset.Parse("2026-10-22T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture))), "step.pr.snapshot", pr);

    private Task<BlockStepResult> Intake(string? pr = null, string? workspace = null) => Run(new PrIntakeStep(_pulls), "step.pr.intake", pr, workspace);

    private async Task<BlockStepResult> Run(IBlockStep step, string id, string? pr, string? workspace = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        if ((pr ?? _pull.Link) is { Length: > 0 } link && pr != "")
        {
            facts["pr"] = link;
        }

        if (workspace is not null)
        {
            facts["workspace"] = workspace;
        }

        return await step.Run(BuildShipContexts.Of(id, facts), Token);
    }

    private static JsonElement Fact(BlockStepResult result, string key) => result.Result!.Outputs.Single(o => string.Equals(o.Key.Value, key, StringComparison.Ordinal)).Value;

    private static string Outcome(BlockStepResult result) => result.Result!.Outcome.Value;

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
        string[] keys = ["thread:1", "thread:2", "thread:3"];
        Assert.Equal(keys, state.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("key").GetString()), StringComparer.Ordinal);
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
        string[] blockedBy = ["reviewRequired"];
        Assert.Equal(blockedBy, Fact(result, "fact:prState").GetProperty("mergeBlockedBy").EnumerateArray().Select(b => b.GetString()), StringComparer.Ordinal);
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
        string[] outputs = ["fact:decision", "fact:fix", "fact:handoffNote", "fact:ownerNote", "fact:recheck", "fact:reply", "fact:workspace"];
        Assert.Equal(
            outputs,
            result.Result!.Outputs.Select(o => o.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Intake_fails_on_a_pull_request_it_cannot_read()
    {
        var result = await Intake(pr: "https://forge.test/acme/app/pull/99");

        Assert.Null(result.Result);
    }
}
