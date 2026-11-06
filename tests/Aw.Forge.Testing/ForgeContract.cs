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

    /// <summary>Seeded check counts show on the pull request; a re-run turns the failed checks into pending ones and a new head clears them.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Check_counts_follow_the_seed_and_the_rerun()
    {
        var (run, pull) = await Open();
        var seed = new SeedChecksRequest(pull, ChecksState.Failing, ["build"], new Dictionary<string, string>(StringComparer.Ordinal), Passed: 3, Pending: 2);

        _ = Expect.Ok(run.Seed!.SeedChecks(seed), "SeedChecks");
        var seeded = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull").Checks;
        _ = Expect.Ok(await run.Forge.RerunChecks(pull, Sha('a'), Ct), "RerunChecks");
        var rerun = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull after the re-run").Checks;
        _ = Expect.Ok(run.Seed.AdvanceHead(pull, Sha('c')), "AdvanceHead");
        var moved = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull after the head moved").Checks;

        Expect.That(seeded.Passed == 3 && seeded.Pending == 2 && seeded.Failing.Count == 1, "the seeded counts show beside the failing names");
        Expect.That(rerun.Passed == 3 && rerun.Pending == 3 && rerun.Failing.Count == 0, "a re-run turns the failed check into a pending one");
        Expect.That(moved.Passed == 0 && moved.Pending == 0, "a new head has no counts");
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

    /// <summary>Seeded files list back in order with a rename's old path and a binary file's missing patch, and the cap flag follows the seed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Files_round_trip_with_a_renamed_and_a_binary_file()
    {
        var (run, pull) = await Open();
        SeedFile[] files =
        [
            new(new PullFile("src/a.cs", PreviousPath: null, PullFileStatus.Modified, 3, 1, "@@ -1,2 +1,4 @@\n+x"), "class A;"),
            new(new PullFile("src/b.cs", "src/old-b.cs", PullFileStatus.Renamed, 0, 0, Patch: null), "class B;"),
            new(new PullFile("assets/logo.png", PreviousPath: null, PullFileStatus.Added, 0, 0, Patch: null), Text: null),
        ];

        _ = Expect.Ok(run.Seed!.SeedFiles(new SeedFilesRequest(pull, files)), "SeedFiles");
        var listed = Expect.Ok(await run.Forge.Files(pull, Ct), "Files");
        _ = Expect.Ok(run.Seed.SeedFiles(new SeedFilesRequest(pull, files, CapReached: true)), "SeedFiles with the cap");
        var capped = Expect.Ok(await run.Forge.Files(pull, Ct), "Files at the cap");

        Expect.That(listed.Files.SequenceEqual(files.Select(f => f.File)) && !listed.CapReached, "the files come back as seeded, in order, with the cap not reached");
        Expect.That(listed.Files[1].PreviousPath == "src/old-b.cs" && listed.Files[1].Status == PullFileStatus.Renamed, "a rename keeps its old path");
        Expect.That(listed.Files[2].Patch is null, "a binary file has no patch");
        Expect.That(capped.CapReached, "the cap flag follows the seed");
        _ = Expect.Fails(await run.Forge.Files(Missing(run), Ct), "AW-FORGE-004", "Files of a missing pull");
    }

    /// <summary>FileText reads a seeded file at the head (and a renamed file's old text at the base), caps at 1 MiB, and is not found for a binary or unknown file.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FileText_reads_seeded_text_and_caps_it()
    {
        var (run, pull) = await Open();
        var big = new string('x', ForgeLimits.FileTextBytes + 100);
        SeedFile[] files =
        [
            new(new PullFile("src/a.cs", PreviousPath: null, PullFileStatus.Modified, 1, 0, "@@"), "class A;", BaseText: "class Old;"),
            new(new PullFile("src/big.txt", PreviousPath: null, PullFileStatus.Added, 1, 0, "@@"), big),
            new(new PullFile("assets/logo.png", PreviousPath: null, PullFileStatus.Added, 0, 0, Patch: null), Text: null),
        ];
        _ = Expect.Ok(run.Seed!.SeedFiles(new SeedFilesRequest(pull, files)), "SeedFiles");
        var snapshot = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull");

        var head = Expect.Ok(await run.Forge.FileText(pull, "src/a.cs", snapshot.Head.Sha, Ct), "FileText at the head");
        var atBase = Expect.Ok(await run.Forge.FileText(pull, "src/a.cs", snapshot.Base.Sha, Ct), "FileText at the base");
        var capped = Expect.Ok(await run.Forge.FileText(pull, "src/big.txt", snapshot.Head.Sha, Ct), "FileText of a big file");

        Expect.That(head == "class A;" && atBase == "class Old;", "the head's and the base's text");
        Expect.That(System.Text.Encoding.UTF8.GetByteCount(capped) <= ForgeLimits.FileTextBytes && capped.Length > 0, "the text is capped at 1 MiB");
        _ = Expect.Fails(await run.Forge.FileText(pull, "assets/logo.png", snapshot.Head.Sha, Ct), "AW-FORGE-004", "FileText of a binary file");
        _ = Expect.Fails(await run.Forge.FileText(pull, "src/none.cs", snapshot.Head.Sha, Ct), "AW-FORGE-004", "FileText of an unknown file");
    }

    /// <summary>Seeded check runs show on Get with their name, app, conclusion and commit.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Check_runs_follow_the_seed()
    {
        var (run, pull) = await Open();
        CheckRun[] runs = [new("build", "github-actions", "success", Sha('a')), new("lint", App: null, "failure", Sha('a'))];
        var seed = new SeedChecksRequest(pull, ChecksState.Failing, ["lint"], new Dictionary<string, string>(StringComparer.Ordinal), Passed: 1, Runs: runs);

        _ = Expect.Ok(run.Seed!.SeedChecks(seed), "SeedChecks with runs");
        var checks = Expect.Ok(await run.Forge.GetPull(pull, Ct), "GetPull").Checks;

        Expect.That(checks.Runs is { } seen && seen.SequenceEqual(runs), "the runs read back as seeded");
    }

    /// <summary>A review posts once per marker and shows in the conversation with its marker.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_review_is_posted_once_per_marker()
    {
        var (run, pull) = await Open();
        var request = new SubmitReviewRequest(pull, Sha('a'), ReviewVerdict.Comment, "Looks fine", [new ReviewComment("src/a.cs", 12, "Nit")], "owner-review:d1:1");
        _ = Expect.Ok(run.Seed!.SeedFiles(new SeedFilesRequest(pull, [new SeedFile(new PullFile("src/a.cs", PreviousPath: null, PullFileStatus.Modified, 3, 0, "@@ -10,2 +10,4 @@\n+x"), "class A;")])), "SeedFiles");

        var first = Expect.Ok(await run.Forge.SubmitReview(request, Ct), "SubmitReview");
        var again = Expect.Ok(await run.Forge.SubmitReview(request, Ct), "SubmitReview again");
        var threads = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads");

        Expect.That(first == ReviewOutcome.Posted && again == ReviewOutcome.AlreadyDelivered, "the second review with the same marker is not posted");
        Expect.That(threads.Where(t => t.Kind == CommentKind.Review && t.Markers.Contains("owner-review:d1:1", StringComparer.Ordinal)).Take(2).Count() == 1, "the review shows once, with its marker");
        _ = Expect.Fails(await run.Forge.SubmitReview(request with { Marker = "has space" }, Ct), "AW-FORGE-001", "SubmitReview with a bad marker");
        _ = Expect.Fails(await run.Forge.SubmitReview(request with { Pull = Missing(run) }, Ct), "AW-FORGE-004", "SubmitReview on a missing pull");
    }

    /// <summary>A review written against a head that has moved is HeadMoved and posts nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_review_on_a_moved_head_is_not_posted()
    {
        var (run, pull) = await Open();
        var request = new SubmitReviewRequest(pull, Sha('a'), ReviewVerdict.RequestChanges, "Stale", [], "owner-review:d2:1");
        _ = Expect.Ok(run.Seed!.AdvanceHead(pull, Sha('c')), "AdvanceHead");

        var outcome = Expect.Ok(await run.Forge.SubmitReview(request, Ct), "SubmitReview on the old head");
        var threads = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads");
        var onNewHead = Expect.Ok(await run.Forge.SubmitReview(request with { ExpectedHead = Sha('c') }, Ct), "SubmitReview on the new head");

        Expect.That(outcome == ReviewOutcome.HeadMoved && !threads.Any(t => t.Kind == CommentKind.Review), "nothing was posted on a stale head");
        Expect.That(onNewHead == ReviewOutcome.Posted, "the same marker posts once the head is right: HeadMoved did not use it up");
    }

    /// <summary>A review whose marker was posted stays delivered after the head moves: the marker is checked before the head.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_posted_review_is_delivered_not_head_moved_after_the_head_advances()
    {
        var (run, pull) = await Open();
        var request = new SubmitReviewRequest(pull, Sha('a'), ReviewVerdict.Comment, "Looks fine", [], "owner-review:d3:1");
        _ = Expect.Ok(await run.Forge.SubmitReview(request, Ct), "SubmitReview");
        _ = Expect.Ok(run.Seed!.AdvanceHead(pull, Sha('c')), "AdvanceHead");

        var again = Expect.Ok(await run.Forge.SubmitReview(request, Ct), "SubmitReview again after the head moved");

        Expect.That(again == ReviewOutcome.AlreadyDelivered, "the same marker is AlreadyDelivered, not HeadMoved");
    }

    /// <summary>A line comment on a path or line the diff does not have is LineRejected and nothing is posted; a line inside a hunk posts.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_comment_outside_the_seeded_patch_is_rejected_and_nothing_is_posted()
    {
        var (run, pull) = await Open();
        SeedFile[] files =
        [
            new(new PullFile("src/a.cs", PreviousPath: null, PullFileStatus.Modified, 3, 0, "@@ -10,2 +10,4 @@\n+x"), "class A;"),
            new(new PullFile("assets/logo.png", PreviousPath: null, PullFileStatus.Added, 0, 0, Patch: null), Text: null),
        ];
        _ = Expect.Ok(run.Seed!.SeedFiles(new SeedFilesRequest(pull, files)), "SeedFiles");
        var before = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads before").Count;
        SubmitReviewRequest Review(string marker, string path, int line) =>
            new(pull, Sha('a'), ReviewVerdict.Comment, "Body", [new ReviewComment(path, line, "Nit")], marker);

        var outside = Expect.Ok(await run.Forge.SubmitReview(Review("owner-review:d4:1", "src/a.cs", 9), Ct), "line before the hunk");
        var past = Expect.Ok(await run.Forge.SubmitReview(Review("owner-review:d4:2", "src/a.cs", 14), Ct), "line past the hunk");
        var unknown = Expect.Ok(await run.Forge.SubmitReview(Review("owner-review:d4:3", "src/none.cs", 10), Ct), "unknown path");
        var binary = Expect.Ok(await run.Forge.SubmitReview(Review("owner-review:d4:4", "assets/logo.png", 1), Ct), "binary file");
        var after = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads after").Count;
        var inside = Expect.Ok(await run.Forge.SubmitReview(Review("owner-review:d4:5", "src/a.cs", 13), Ct), "last line of the hunk");

        Expect.That(new[] { outside, past, unknown, binary }.All(o => o == ReviewOutcome.LineRejected), "each line the diff lacks is LineRejected");
        Expect.That(after == before, "nothing was posted by the rejected reviews");
        Expect.That(inside == ReviewOutcome.Posted, "a line inside the hunk posts (10 to 13)");
    }

    /// <summary>A comment on a seeded file with no patch (a binary file) is LineRejected, on any line, and nothing is posted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_comment_on_a_file_without_a_patch_is_rejected_and_nothing_is_posted()
    {
        var (run, pull) = await Open();
        SeedFile[] files = [new(new PullFile("assets/logo.png", PreviousPath: null, PullFileStatus.Added, 0, 0, Patch: null), Text: null)];
        _ = Expect.Ok(run.Seed!.SeedFiles(new SeedFilesRequest(pull, files)), "SeedFiles");
        var before = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads before").Count;
        var request = new SubmitReviewRequest(pull, Sha('a'), ReviewVerdict.Comment, "Body", [new ReviewComment("assets/logo.png", 1, "Nit")], "owner-review:d6:1");

        var outcome = Expect.Ok(await run.Forge.SubmitReview(request, Ct), "SubmitReview on a binary file");
        var after = Expect.Ok(await run.Forge.ListThreads(pull, Ct), "ListThreads after").Count;

        Expect.That(outcome == ReviewOutcome.LineRejected, "a file with no patch rejects every line");
        Expect.That(after == before, "nothing was posted");
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
