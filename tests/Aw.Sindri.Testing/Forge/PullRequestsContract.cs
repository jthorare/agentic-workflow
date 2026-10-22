using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Xunit;

namespace Aw.Sindri.Testing.Forge;

/// <summary>
/// The contract every <see cref="IPullRequests"/> honours once a forge is configured: a pull request opened through the port reads back, Find answers none
/// before Open and the pull request after, a marker keeps two pull requests apart, a host outside the list is refused before anything is sent, and a missing
/// pull request is SND-FORGE-003. Run against the fake and, over the file forge, the real adapter.
/// </summary>
public abstract class PullRequestsContract
{
    private const string Feature = "feature/contract";

    /// <summary>A port, with a repository on an allowed host and a host that is not allowed.</summary>
    /// <returns>The port ready for a run.</returns>
    protected abstract PullRequestsUnderTest Create();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A pull request opened through the port reads back open, at its head, with no checks and no threads.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_opened_pull_request_reads_back()
    {
        var (run, opened) = await Open();

        var view = Ok(await run.Pulls.Get(opened.Link, Ct), "Get");

        Expect.Equal(opened.Link, view.Link);
        Expect.Equal(PullState.Open, view.State);
        Expect.Equal(Sha('a'), view.HeadSha);
        Expect.Equal(PullChecksState.None, view.Checks.State);
        Expect.Equal(0, view.UnresolvedThreads);
    }

    /// <summary>Find answers a success with nothing before a pull request is opened, and the pull request after.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Find_answers_none_then_the_opened_pull_request()
    {
        var run = Create();
        var query = new FindPull(run.Repo, Feature, "run-1");

        var before = Ok(await run.Pulls.Find(query, Ct), "Find before");
        var opened = Ok(await run.Pulls.Open(Request(run, marker: "run-1"), Ct), "Open");
        var after = Ok(await run.Pulls.Find(query, Ct), "Find after");

        Expect.True(before is null);
        Expect.Equal(opened, after);
        Expect.True(opened.Number >= 1);
    }

    /// <summary>A different marker or head finds nothing, so a retried effect never takes another run's pull request.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Find_keeps_markers_and_heads_apart()
    {
        var (run, opened) = await Open(marker: "run-1");

        var otherMarker = Ok(await run.Pulls.Find(new FindPull(run.Repo, Feature, "run-2"), Ct), "Find by another marker");
        var otherHead = Ok(await run.Pulls.Find(new FindPull(run.Repo, "feature/other", Marker: null), Ct), "Find by another head");
        var byHead = Ok(await run.Pulls.Find(new FindPull(run.Repo, Feature, Marker: null), Ct), "Find by head alone");

        Expect.True(otherMarker is null && otherHead is null);
        Expect.Equal(opened, byHead);
    }

    /// <summary>Threads lists none for a new pull request and, after a reviewer comments, the thread with its key, author and untouched body.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Threads_lists_what_a_reviewer_wrote()
    {
        var (run, opened) = await Open();

        var before = Ok(await run.Pulls.Threads(opened.Link, Ct), "Threads before");
        var key = run.SeedThread(opened.Link, "Please fix this");
        var after = Ok(await run.Pulls.Threads(opened.Link, Ct), "Threads after");
        var view = Ok(await run.Pulls.Get(opened.Link, Ct), "Get");

        Expect.Equal(0, before.Count);
        var thread = Expect.Only(after);
        Expect.Equal(key, thread.Key);
        Expect.Equal(PullCommentKind.Thread, thread.Kind);
        Expect.Equal("reviewer", thread.Author);
        Expect.Equal("Please fix this", thread.Body);
        Expect.True(!thread.Resolved && !thread.IsBot);
        Expect.Equal(1, view.UnresolvedThreads);
        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.Threads(MissingLink(run), Ct)));
    }

    /// <summary>A merge with the right head lands and reads back merged; a stale head is HeadMoved and changes nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_merge_checks_the_expected_head()
    {
        var (run, opened) = await Open();

        var moved = Ok(await run.Pulls.Merge(new PullMerge(opened.Link, PullMergeMethod.Squash, Sha('b')), Ct), "Merge with a stale head");
        var merged = Ok(await run.Pulls.Merge(new PullMerge(opened.Link, PullMergeMethod.Squash, Sha('a')), Ct), "Merge");
        var view = Ok(await run.Pulls.Get(opened.Link, Ct), "Get");

        Expect.Equal(PullMergeStatus.HeadMoved, moved.Status);
        Expect.Equal(PullMergeStatus.Merged, merged.Status);
        Expect.True(!string.IsNullOrEmpty(merged.MergedSha));
        Expect.Equal(PullState.Merged, view.State);
        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.Merge(new PullMerge(MissingLink(run), PullMergeMethod.Merge, Sha('a')), Ct)));
    }

    /// <summary>A reply or a reaction posts once per marker.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reply_and_a_reaction_are_delivered_once_per_marker()
    {
        var (run, opened) = await Open();
        var key = run.SeedThread(opened.Link, "Please fix this");

        var first = Ok(await run.Pulls.Reply(new PullReply(opened.Link, key, "Fixed", "r1"), Ct), "Reply");
        var again = Ok(await run.Pulls.Reply(new PullReply(opened.Link, key, "Fixed", "r1"), Ct), "Reply again");
        var onPull = Ok(await run.Pulls.Reply(new PullReply(opened.Link, "pull", "Summary", "s1"), Ct), "Reply on the pull request");
        var reacted = Ok(await run.Pulls.React(new PullReaction(opened.Link, key, "+1", "x1"), Ct), "React");
        var reactedAgain = Ok(await run.Pulls.React(new PullReaction(opened.Link, key, "+1", "x1"), Ct), "React again");

        Expect.Equal(PullDelivery.Delivered, first);
        Expect.Equal(PullDelivery.AlreadyDelivered, again);
        Expect.Equal(PullDelivery.Delivered, onPull);
        Expect.Equal(PullDelivery.Delivered, reacted);
        Expect.Equal(PullDelivery.AlreadyDelivered, reactedAgain);
    }

    /// <summary>A branch pushed before any pull request exists can then be opened and found; the lease rules decide a second push.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_pushed_branch_opens_and_is_found()
    {
        var run = Create();
        if (run.CanPush)
        {
            var created = Ok(await run.Pulls.PushBranch(new PullBranchPush(run.Repo, "/unused", Feature, Sha('a'), LeaseSha: null), Ct), "PushBranch of a new branch");
            var exists = Ok(await run.Pulls.PushBranch(new PullBranchPush(run.Repo, "/unused", Feature, Sha('b'), LeaseSha: null), Ct), "PushBranch of an existing branch with no lease");
            var stale = Ok(await run.Pulls.PushBranch(new PullBranchPush(run.Repo, "/unused", Feature, Sha('b'), Sha('z')), Ct), "PushBranch with a stale lease");
            var leased = Ok(await run.Pulls.PushBranch(new PullBranchPush(run.Repo, "/unused", Feature, Sha('c'), Sha('a')), Ct), "PushBranch with the right lease");

            Expect.Equal(PullPushOutcome.Pushed, created);
            Expect.Equal(PullPushOutcome.HeadMoved, exists);
            Expect.Equal(PullPushOutcome.HeadMoved, stale);
            Expect.Equal(PullPushOutcome.Pushed, leased);
        }

        var opened = Ok(await run.Pulls.Open(Request(run, "run-1"), Ct), "Open");
        var found = Ok(await run.Pulls.Find(new FindPull(run.Repo, Feature, "run-1"), Ct), "Find");

        Expect.Equal(opened, found);
    }

    /// <summary>Update succeeds on a pull request and answers SND-FORGE-003 for one that isn't there.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Update_changes_a_pull_request_and_refuses_a_missing_one()
    {
        var (run, opened) = await Open();

        _ = Ok(await run.Pulls.Update(opened.Link, "New title", body: null, Ct), "Update");
        var missing = await run.Pulls.Update(MissingLink(run), "New title", body: null, Ct);

        Expect.Equal("SND-FORGE-003", Failure(missing));
    }

    /// <summary>A pull request that isn't there is SND-FORGE-003 on every read.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_pull_request_is_not_found()
    {
        var run = Create();

        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.Get(MissingLink(run), Ct)));
        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.CheckLog(MissingLink(run), "build", Ct)));
        Expect.Equal("SND-FORGE-003", Failure(await run.Pulls.RerunChecks(MissingLink(run), Sha('a'), Ct)));
    }

    /// <summary>A host outside the list is SND-FORGE-002 on a link, a find and an open, and nothing is created.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_host_outside_the_list_is_refused()
    {
        var run = Create();
        var foreign = new PullRepo(run.ForeignHost, run.Repo.Owner, run.Repo.Name);

        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Get($"https://{run.ForeignHost}/{run.Repo.Owner}/{run.Repo.Name}/pull/1", Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Find(new FindPull(foreign, Feature, Marker: null), Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Open(Request(run) with { Repo = foreign }, Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.Get("not a link", Ct)));
        Expect.Equal("SND-FORGE-002", Failure(await run.Pulls.PushBranch(new PullBranchPush(foreign, "/unused", Feature, Sha('a'), LeaseSha: null), Ct)));
    }

    private static string Sha(char c) => new(c, 40);

    private static OpenPull Request(PullRequestsUnderTest run, string? marker = null) =>
        new(run.Repo, Feature, "main", Sha('a'), "Title", "Body", Draft: false, marker);

    private static string MissingLink(PullRequestsUnderTest run) => $"https://{run.Repo.Host}/{run.Repo.Owner}/{run.Repo.Name}/pull/424242";

    private static T Ok<T>(Result<T> result, string what) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"contract: {what} failed with {result.Error.Code}: {result.Error.Message}");

    private static string Failure<T>(Result<T> result) =>
        result.IsError ? result.Error.Code : throw new ContractViolationException("contract: expected an error");

    private async Task<(PullRequestsUnderTest Run, OpenedPull Opened)> Open(string? marker = null)
    {
        var run = Create();
        return (run, Ok(await run.Pulls.Open(Request(run, marker), Ct), "Open"));
    }
}
