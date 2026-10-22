using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;
using Xunit;

namespace Aw.Forge.Testing;

/// <summary>
/// The whole IForge and IForgeSeed contract: the read side from <see cref="ForgeReadContract"/> plus every write and the
/// seeding that lets a test play the other side. The same tests run against the fake and the file forge.
/// </summary>
public abstract class ForgeContract : ForgeReadContract
{
    private const string Feature = "feature/contract";

    /// <summary>An opened pull request reads back with what it was opened with.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_opened_pull_request_reads_back()
    {
        var (run, pull) = await Open(draft: true);

        var snapshot = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull");

        Expect.That(snapshot.Title == "Title" && snapshot.Status == PullStatus.Open && snapshot.Draft, "title, open status and draft flag");
        Expect.That(snapshot.Head.Ref == Feature && snapshot.Head.Sha == Sha('a') && snapshot.Base.Ref == "main", "head and base");
        Expect.That(snapshot.Ref == pull && snapshot.Url == pull.Link, "ref and link");
    }

    /// <summary>FindPull matches the head branch, the author and a marker in the body.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FindPull_matches_head_and_marker()
    {
        var (run, pull) = await Open(body: ForgeMarker.Append("Body", "run-1"));

        var byMarker = Expect.Ok(await run.Forge.FindPull(new FindPullQuery(run.Repo, Feature, Author: null, Marker: "run-1"), Ct), "FindPull by marker");
        var otherMarker = Expect.Ok(await run.Forge.FindPull(new FindPullQuery(run.Repo, Feature, Author: null, Marker: "run-2"), Ct), "FindPull by another marker");
        var otherHead = Expect.Ok(await run.Forge.FindPull(new FindPullQuery(run.Repo, "feature/other", Author: null, Marker: null), Ct), "FindPull by another head");

        Expect.That(byMarker == pull, "found by head and marker");
        Expect.That(otherMarker is null && otherHead is null, "a different marker or head finds nothing");
    }

    /// <summary>ListThreads returns threads, review bodies and comments oldest first whatever order they were added in, each with its time, line, edit time and link.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Threads_come_oldest_first_across_kinds_with_their_fields()
    {
        var (run, pull) = await Open();
        var at = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var review = Expect.Ok(run.Seed!.SeedThread(new SeedThreadRequest(pull, CommentKind.Review, "reviewer", "MEMBER", IsBot: false, Path: null, Body: "Looks fine", CreatedAt: at.AddHours(1))), "SeedThread review");
        var comment = Expect.Ok(run.Seed.SeedThread(new SeedThreadRequest(pull, CommentKind.Comment, "reviewer", "MEMBER", IsBot: false, Path: null, Body: "Thanks", CreatedAt: at.AddHours(2))), "SeedThread comment");
        var inline = Expect.Ok(run.Seed.SeedThread(new SeedThreadRequest(pull, CommentKind.Thread, "reviewer", "MEMBER", IsBot: false, Path: "src/a.cs", Body: "Please fix this", CreatedAt: at, Line: 12, EditedAt: at.AddMinutes(30))), "SeedThread inline");

        var threads = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads");

        Expect.That(threads.Select(t => t.Key).SequenceEqual([inline, review, comment], StringComparer.Ordinal), "oldest first across kinds, not in the order added");
        var first = threads[0];
        Expect.That(first.CreatedAt == at && first.Line == 12 && first.EditedAt == at.AddMinutes(30), "the thread carries its time, line and edit time");
        Expect.That(threads[1].Line is null && threads[1].EditedAt is null, "a review body has no line and was not edited");
        Expect.That(threads.All(t => t.Url is not null && t.Url.StartsWith(pull.Link, StringComparison.Ordinal) && t.Url.EndsWith(t.Key, StringComparison.Ordinal)), "each links to the pull request");
    }

    /// <summary>Labels given to OpenPull are on the snapshot; none given means none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Opened_labels_read_back()
    {
        var run = Create();
        var labelled = Expect.Ok(await run.Forge.OpenPull(new OpenPullRequest(run.Repo, Feature, "main", Sha('a'), "T", "B", Draft: false, Labels: ["area:forge", "size:S"]), Ct), "OpenPull with labels");
        var plain = Expect.Ok(await run.Forge.OpenPull(new OpenPullRequest(run.Repo, "feature/plain", "main", Sha('a'), "T", "B", Draft: false), Ct), "OpenPull without labels");

        var withLabels = Expect.Ok(await run.Forge.GetPull(labelled, Ct), "GetPull").Labels;
        var without = Expect.Ok(await run.Forge.GetPull(plain, Ct), "GetPull").Labels;

        Expect.That(withLabels.Order(StringComparer.Ordinal).SequenceEqual(["area:forge", "size:S"], StringComparer.Ordinal) && without.Count == 0, "the labels given are the labels read, none by default");
    }

    /// <summary>Self names the login the forge acts as.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Self_names_the_acting_login()
    {
        var run = Create();

        var login = Expect.Ok(await run.Forge.Self(Ct), "Self");

        Expect.That(login.Length > 0, "a login");
    }

    /// <summary>UpdatePull changes the fields it is given and keeps the others; Retarget moves the base.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Update_and_retarget_change_the_pull_request()
    {
        var (run, pull) = await Open();

        _ = Expect.Ok(await run.Forge.UpdatePull(new UpdatePullRequest(pull, "New title", Body: null), Ct), "UpdatePull");
        _ = Expect.Ok(await run.Forge.Retarget(pull, "release", Ct), "Retarget");
        var snapshot = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull");

        Expect.That(snapshot.Title == "New title", "the title changed");
        Expect.That(snapshot.Base.Ref == "release", "the base moved");
        _ = Expect.Fails(await run.Forge.UpdatePull(new UpdatePullRequest(Missing(run), "x", Body: null), Ct), "AW-FORGE-004", "UpdatePull of a missing pull");
    }

    /// <summary>A reply lands once per marker, on a thread or on the pull request itself.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reply_is_posted_once_per_marker()
    {
        var (run, pull) = await Open();
        var key = Expect.Ok(run.Seed!.SeedThread(Thread(pull)), "SeedThread");

        var first = Expect.Ok(await run.Forge.Reply(new ReplyRequest(pull, key, "Fixed", "r1"), Ct), "Reply");
        var again = Expect.Ok(await run.Forge.Reply(new ReplyRequest(pull, key, "Fixed", "r1"), Ct), "Reply again");
        var onPull = Expect.Ok(await run.Forge.Reply(new ReplyRequest(pull, "pull", "Summary", "s1"), Ct), "Reply on the pull");
        var onPullAgain = Expect.Ok(await run.Forge.Reply(new ReplyRequest(pull, "pull", "Summary", "s1"), Ct), "Reply on the pull again");
        var threads = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads");

        Expect.That(first == Delivery.Delivered && again == Delivery.AlreadyDelivered, "the second reply with the same marker is not posted");
        Expect.That(onPull == Delivery.Delivered && onPullAgain == Delivery.AlreadyDelivered, "the same holds on the pull request");
        Expect.That(threads.Any(t => t.Key == key && t.Markers.Contains("r1", StringComparer.Ordinal)), "the thread carries the marker");
        Expect.That(threads.Any(t => t.Kind == CommentKind.Comment && t.Markers.Contains("s1", StringComparer.Ordinal)), "the pull comment carries its marker");
    }

    /// <summary>A reply to a thread that isn't there is AW-FORGE-004; a marker that isn't valid is AW-FORGE-001.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reply_checks_its_thread_and_marker()
    {
        var (run, pull) = await Open();
        var key = Expect.Ok(run.Seed!.SeedThread(Thread(pull)), "SeedThread");

        _ = Expect.Fails(await run.Forge.Reply(new ReplyRequest(pull, "thread:999", "x", "m"), Ct), "AW-FORGE-004", "Reply to a missing thread");
        _ = Expect.Fails(await run.Forge.Reply(new ReplyRequest(pull, key, "x", "has space"), Ct), "AW-FORGE-001", "Reply with a bad marker");
        _ = Expect.Fails(await run.Forge.React(new ReactionRequest(pull, key, "+1", string.Empty), Ct), "AW-FORGE-001", "React with an empty marker");
    }

    /// <summary>A reaction lands once per marker.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reaction_is_added_once_per_marker()
    {
        var (run, pull) = await Open();
        var key = Expect.Ok(run.Seed!.SeedThread(Thread(pull)), "SeedThread");

        var first = Expect.Ok(await run.Forge.React(new ReactionRequest(pull, key, "+1", "x1"), Ct), "React");
        var again = Expect.Ok(await run.Forge.React(new ReactionRequest(pull, key, "+1", "x1"), Ct), "React again");

        Expect.That(first == Delivery.Delivered && again == Delivery.AlreadyDelivered, "the second reaction with the same marker is not added");
    }

    /// <summary>Seeded checks show on the pull request, a log is capped and read back, and a re-run clears the failures.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Checks_logs_and_reruns_follow_the_seed()
    {
        var (run, pull) = await Open();
        var log = new string('x', ForgeLimits.LogExcerptCharacters + 500) + "TAIL";
        var seed = new SeedChecksRequest(pull, ChecksState.Failing, ["build"], new Dictionary<string, string>(StringComparer.Ordinal) { ["build"] = log });

        _ = Expect.Ok(run.Seed!.SeedChecks(seed), "SeedChecks");
        var failing = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull").Checks;
        var excerpt = Expect.Ok(await run.Forge.CheckLog(pull, "build", Ct), "CheckLog");
        _ = Expect.Fails(await run.Forge.CheckLog(pull, "no-such-check", Ct), "AW-FORGE-004", "CheckLog of an unknown check");
        var stale = Expect.Ok(await run.Forge.RerunChecks(pull, Sha('z'), Ct), "RerunChecks on another commit");
        var rerun = Expect.Ok(await run.Forge.RerunChecks(pull, Sha('a'), Ct), "RerunChecks");
        var after = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull after the re-run").Checks;

        Expect.That(failing.State == ChecksState.Failing && failing.Failing.Contains("build", StringComparer.Ordinal) && failing.Sha == Sha('a'), "the failing check shows with its commit");
        Expect.That(excerpt.Length <= ForgeLimits.LogExcerptCharacters && excerpt.EndsWith("TAIL", StringComparison.Ordinal), "the log is the capped tail");
        Expect.That(stale == 0 && rerun == 1, "only the failed checks of that commit are re-run");
        Expect.That(after.State == ChecksState.Pending && after.Failing.Count == 0, "re-run checks are pending");
    }

    /// <summary>The digest's version changes with the pull request, and its comment count follows the threads.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_digest_version_changes_when_the_pull_request_does()
    {
        var (run, pull) = await Open();
        var before = Expect.Ok(await run.Forge.GetPullDigest([pull], Ct), "GetPullDigest")[0];

        _ = Expect.Ok(run.Seed!.SeedThread(Thread(pull)), "SeedThread");
        var after = Expect.Ok(await run.Forge.GetPullDigest([pull], Ct), "GetPullDigest after");
        var same = Expect.Ok(await run.Forge.GetPullDigest([pull], Ct), "GetPullDigest again");

        Expect.That(after.Count == 1 && after[0].Version != before.Version, "a new thread changes the version");
        Expect.That(after[0].CommentCount == before.CommentCount + 1 && after[0].Version == same[0].Version, "the count grows and an unchanged pull request keeps its version");
        Expect.That(after[0].HeadSha == Sha('a') && after[0].Status == PullStatus.Open, "head and status");
    }

    /// <summary>A merge checks the expected head and the pull request's state: HeadMoved, Blocked, then Merged once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_merge_checks_the_expected_head()
    {
        var (run, pull) = await Open();
        _ = Expect.Ok(run.Seed!.SeedChecks(new SeedChecksRequest(pull, ChecksState.Failing, ["build"], new Dictionary<string, string>(StringComparer.Ordinal))), "SeedChecks");

        var moved = Expect.Ok(await run.Forge.Merge(new MergeRequest(pull, MergeMethod.Squash, Sha('b')), Ct), "Merge with a stale head");
        var blocked = Expect.Ok(await run.Forge.Merge(new MergeRequest(pull, MergeMethod.Squash, Sha('a')), Ct), "Merge with failing checks");
        _ = Expect.Ok(run.Seed.SeedChecks(new SeedChecksRequest(pull, ChecksState.Passing, [], new Dictionary<string, string>(StringComparer.Ordinal))), "SeedChecks passing");
        var merged = Expect.Ok(await run.Forge.Merge(new MergeRequest(pull, MergeMethod.Squash, Sha('a')), Ct), "Merge");
        var twice = Expect.Ok(await run.Forge.Merge(new MergeRequest(pull, MergeMethod.Squash, Sha('a')), Ct), "Merge again");
        var snapshot = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull");

        Expect.That(moved.Status == MergeStatus.HeadMoved, "a stale expected head is HeadMoved");
        Expect.That(blocked.Status == MergeStatus.Blocked && !string.IsNullOrEmpty(blocked.Reason), "failing checks block, with the reason");
        Expect.That(merged.Status == MergeStatus.Merged && !string.IsNullOrEmpty(merged.MergedSha) && snapshot.Status == PullStatus.Merged, "the merge lands");
        Expect.That(twice.Status == MergeStatus.Blocked, "a merged pull request cannot merge again");
    }

    /// <summary>A foreign push (AdvanceHead) moves the head and clears the checks, so a merge on the old head is HeadMoved.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_advanced_head_moves_the_pull_request()
    {
        var (run, pull) = await Open();
        _ = Expect.Ok(run.Seed!.SeedChecks(new SeedChecksRequest(pull, ChecksState.Passing, [], new Dictionary<string, string>(StringComparer.Ordinal))), "SeedChecks");

        _ = Expect.Ok(run.Seed.AdvanceHead(pull, Sha('c')), "AdvanceHead");
        var snapshot = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull");
        var merge = Expect.Ok(await run.Forge.Merge(new MergeRequest(pull, MergeMethod.Merge, Sha('a')), Ct), "Merge on the old head");

        Expect.That(snapshot.Head.Sha == Sha('c') && snapshot.Checks.State == ChecksState.None, "the head moved and the checks cleared");
        Expect.That(merge.Status == MergeStatus.HeadMoved, "a merge on the old head is HeadMoved");
        _ = Expect.Fails(Result(run.Seed.AdvanceHead(Missing(run), Sha('d'))), "AW-FORGE-004", "AdvanceHead of a missing pull");
    }

    /// <summary>A push takes its lease on the branch's tip: Pushed, then HeadMoved for a missing or stale lease, and a pull request follows its branch.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_push_takes_a_lease_on_the_branch_tip()
    {
        var run = Create();
        if (!run.CanPush)
        {
            return;   // a real-git push needs a real repository: the Infrastructure tests cover it
        }

        var first = Expect.Ok(await run.Forge.Push(Push(Sha('a'), lease: null), Ct), "first Push");
        var secondWithoutLease = Expect.Ok(await run.Forge.Push(Push(Sha('b'), lease: null), Ct), "Push with no lease on an existing branch");
        var stale = Expect.Ok(await run.Forge.Push(Push(Sha('b'), lease: Sha('z')), Ct), "Push with a stale lease");
        var pull = Expect.Ok(await run.Forge.OpenPull(new OpenPullRequest(run.Repo, Feature, "main", Sha('a'), "T", "B", Draft: false), Ct), "OpenPull");
        var leased = Expect.Ok(await run.Forge.Push(Push(Sha('c'), lease: Sha('a')), Ct), "Push with the right lease");
        var snapshot = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull");

        Expect.That(first == PushOutcome.Pushed, "a new branch is pushed");
        Expect.That(secondWithoutLease == PushOutcome.HeadMoved && stale == PushOutcome.HeadMoved, "no lease on an existing branch, or a stale one, is HeadMoved");
        Expect.That(leased == PushOutcome.Pushed && snapshot.Head.Sha == Sha('c'), "the right lease pushes and the pull request follows its branch");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PushRequest Push(string sha, string? lease) => new("/unused", sha, "https://forge.test/acme/widgets.git", Feature, lease);

    private static SeedThreadRequest Thread(ForgeRef pull) =>
        new(pull, CommentKind.Thread, "reviewer", "MEMBER", IsBot: false, Path: "src/a.cs", Body: "Please fix this");

    private static ForgeRef Missing(ForgeUnderTest run) => new(run.Forge.Kind, run.Repo.Host, run.Repo.Owner, run.Repo.Name, 424242);

    private static Result<Unit> Result(Result<Unit> result) => result;

    private async Task<(ForgeUnderTest Run, ForgeRef Pull)> Open(bool draft = false, string body = "Body")
    {
        var run = Create();
        Expect.That(run.Seed is not null, "this contract needs a forge that can be seeded");
        var pull = Expect.Ok(await run.Forge.OpenPull(new OpenPullRequest(run.Repo, Feature, "main", Sha('a'), "Title", body, draft), Ct), "OpenPull");
        return (run, pull);
    }
}
