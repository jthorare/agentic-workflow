using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The PR page's model from its facts (PR review page spec §6.2, §6.3): header, strip, panels in the settings' order, states and the grouped diff.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageBuilderTests
{
    private const string Head = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private const string Url = "https://github.com/acme/api/pull/2071";
    private static readonly DateTimeOffset _at = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReviewPageLibraryFacts NoLibrary(string? error = null, int hidden = 0) => new(error, [], Doc: null, [new ReviewPageProjectDoc("sindri-pr-review", "Sindri PR review page", "docs/x.md")], [], hidden);

    private static PullFile File(string path, PullFileStatus status = PullFileStatus.Modified, int add = 1, int del = 0, string? patch = "@@ -1 +1 @@\n-a\n+b", string? previous = null, int? similarity = null) =>
        new(path, previous, status, add, del, patch, similarity);

    private static ReviewPageFacts Facts(
        PrReviewResolved? settings = null, string author = "sam-dev", string? self = null, IReadOnlyList<PullFile>? files = null, bool capped = false, string? filesError = null,
        IReadOnlyList<PullThread>? threads = null, string? threadsError = null, IReadOnlyList<PullCheckRun>? runs = null, string? runsError = null,
        ReviewPageLibraryFacts? library = null, IReadOnlyList<ReviewPageEvidenceItem>? evidence = null, string? body = null, bool moving = false, string[]? ticked = null) =>
        new(
            new AsOf(1, 1, _at, Rebuilding: false, Stalled: false), "acme/api", 2071, "joi-example", self,
            new ReviewPagePrFacts("Add retry budget", author, Draft: false, "open", Head, "feat/x", "main", Url, body, ["ENG-12"], _at),
            new ReviewPageSettingsFacts(settings ?? PrReviewDefaults.Resolve(review: null, "default")!, "default", "default", Choices: []),
            new ReviewPageRead<PullFiles>(new PullFiles(files ?? [], capped), filesError),
            new ReviewPageRead<IReadOnlyList<PullThread>>(threads ?? [], threadsError),
            new ReviewPageRead<IReadOnlyList<PullCheckRun>>(runs ?? [], runsError),
            library ?? NoLibrary(),
            new ReviewPageRead<IReadOnlyList<ReviewPageEvidenceItem>>(evidence ?? []),
            new HashSet<string>(ticked ?? [], StringComparer.Ordinal), moving);

    private static ReviewPagePanel Panel(ReviewPageModel page, string id) => Assert.Single(page.Panels, p => p.Key == id);

    private static PrReviewResolved With(string[]? panels = null, PrReviewCheck[]? checks = null, PrReviewGroup[]? groups = null, string[]? collapse = null, PrReviewRisky[]? risky = null) =>
        new("t", panels ?? PrReviewDefaults.Panels, groups ?? [], collapse ?? [], risky ?? [], checks ?? [], new Dictionary<string, string>(StringComparer.Ordinal));

    [Fact]
    public void The_header_carries_the_head_and_the_linear_chips()
    {
        var pr = ReviewPageBuilder.Build(Facts()).Pr;

        Assert.Equal(("acme/api", 2071, Head[..7], "feat/x", "sindri://linear/ENG-12"), (pr.Repo, pr.Number, pr.Head.Sha7, pr.Head.Branch, Assert.Single(pr.Issues).Href));
    }

    [Theory]
    [InlineData("Joi-Example", null, true, true)]
    [InlineData("sam-dev", "sam-dev", false, true)]
    [InlineData("sam-dev", "SINDRI", false, false)]
    [InlineData("sam-dev", null, false, false)]
    public void Yours_and_posts_fix_threads_follow_the_login_and_the_forge_credentials_own_login(string author, string? self, bool yours, bool posts)
    {
        var pr = ReviewPageBuilder.Build(Facts(author: author, self: self)).Pr;

        Assert.Equal((yours, posts), (pr.Yours, pr.PostsFixThreads));
    }

    [Fact]
    public void Panels_follow_the_settings_order_and_a_panel_left_out_is_absent()
    {
        var page = ReviewPageBuilder.Build(Facts(With(panels: ["risk", "ci"])));

        Assert.Equal("risk,ci", string.Join(',', page.Panels.Select(static p => p.Key)));
    }

    [Fact]
    public void Empty_panels_say_their_state_in_the_specs_words()
    {
        var page = ReviewPageBuilder.Build(Facts());

        Assert.Equal(
            [
                ("requiredChecks", "empty", "No required checks for this project"), ("docMap", "empty", "This PR names no design doc"), ("testPlan", "empty", "No test plan rows for the claimed items"),
                ("ci", "empty", "No CI runs on a1b2c3d yet"), ("evidence", "empty", "No evidence found for a1b2c3d"), ("risk", "empty", "No risky paths touched"), ("conversation", "empty", "No threads yet"),
            ],
            page.Panels.Select(static p => (p.Key, p.State, p.Text)));
        Assert.Equal(("empty", "This PR changes no files"), (page.Diff.State, page.Diff.Text));
    }

    [Fact]
    public void An_empty_doc_map_offers_the_projects_docs()
    {
        var data = Assert.IsType<DocMapData>(Panel(ReviewPageBuilder.Build(Facts()), "docMap").Data);

        Assert.Equal("sindri-pr-review", Assert.Single(data.ProjectDocs).Key);
    }

    [Fact]
    public void Failed_reads_make_error_panels_with_the_forges_reason()
    {
        var page = ReviewPageBuilder.Build(Facts(filesError: "github returned 502", threadsError: "github returned 502", runsError: "github sent no check runs", library: NoLibrary("/home/joi/docs can't be read")));

        Assert.Equal(
            [
                ("requiredChecks", "error", "Couldn't decide checks: github failed: github returned 502"), ("docMap", "error", "Couldn't read the library: /home/joi/docs can't be read"),
                ("testPlan", "error", "Couldn't read the library: /home/joi/docs can't be read"), ("ci", "error", "Couldn't read CI: github sent no check runs"), ("conversation", "error", "Couldn't read threads: github returned 502"),
            ],
            page.Panels.Where(static p => p.State == "error").Select(static p => (p.Key, p.State, p.Text)));
        Assert.Equal(("error", "Couldn't read the diff: github returned 502"), (page.Diff.State, page.Diff.Text));
    }

    [Fact]
    public void Required_checks_come_from_the_evaluator_and_only_a_fail_offers_a_fix_task()
    {
        var checks = new[]
        {
            new PrReviewCheck("ui", "Ui tests ran", When: null, new PrReviewNeed(Ci: "ui-tests")),
            new PrReviewCheck("mine", "I ran it", When: null, new PrReviewNeed(Manual: true)),
            new PrReviewCheck("proof", "Proof in the body", When: null, new PrReviewNeed(Body: "^proof$")),
            new PrReviewCheck("lint", "Lint", When: null, new PrReviewNeed(Ci: "lint")),
        };
        var page = ReviewPageBuilder.Build(Facts(With(checks: checks), runs: [new PullCheckRun("ui-tests", "actions", "failure", Head), new PullCheckRun("lint", "actions", "success", Head)], body: "proof"));

        var rows = Assert.IsType<RequiredChecksData>(Panel(page, "requiredChecks").Data).Checks;

        Assert.Equal(
            [("ui", "fail", "ui-tests: failure on a1b2c3d", true), ("mine", "needsTick", null, false), ("proof", "claimed", "claimed in body", false), ("lint", "pass", "lint: success on a1b2c3d", false)],
            rows.Select(static c => (c.Key, c.Status, c.Detail, c.FixTask)));
        Assert.Equal("3 open", page.Strip.Single(static s => s.Panel == "requiredChecks").Text);
    }

    [Fact]
    public void A_manual_check_ticked_on_this_head_is_ticked_and_not_open()
    {
        var page = ReviewPageBuilder.Build(Facts(With(checks: [new PrReviewCheck("mine", "I ran it", When: null, new PrReviewNeed(Manual: true))]), ticked: ["mine"]));

        Assert.Equal(("ticked", "none open"), (Assert.Single(Assert.IsType<RequiredChecksData>(Panel(page, "requiredChecks").Data).Checks).Status, page.Strip[0].Text));
    }

    [Fact]
    public void The_strip_names_the_claim_the_risky_files_and_the_failing_runs_and_drops_what_has_nothing_to_say()
    {
        var doc = new ReviewPageDocFact("d", "Doc", "docs/d.md", [new ReviewPageItemFact("Ph5", "Routes", "phase", Parent: null, ClaimedHere: true, ClaimedBy: []), new ReviewPageItemFact("REQ-E3", "Every route", "req", "Ph5", ClaimedHere: true, ClaimedBy: [])]);
        var library = NoLibrary() with { Doc = doc, Claims = [new ReviewPageClaim("d#Ph5", "resolved", Error: null), new ReviewPageClaim("d#REQ-E3", "resolved", Error: null)] };
        var risky = new[] { new PrReviewRisky(["db/**"], "Migrations change stored data") };
        var page = ReviewPageBuilder.Build(Facts(With(risky: risky), files: [File("db/1.sql"), File("src/a.cs")], library: library, runs: [new PullCheckRun("ui", "actions", "failure", Head), new PullCheckRun("lint", "actions", "success", Head)]));

        Assert.Equal([("requiredChecks", "none open"), ("docMap", "Covers Ph5, REQ-E3"), ("risk", "1 risky file"), ("ci", "1 failing")], page.Strip.Select(static s => (s.Panel, s.Text)));
    }

    [Fact]
    public void The_strip_says_no_doc_named_and_couldnt_decide_checks()
    {
        var page = ReviewPageBuilder.Build(Facts(filesError: "github returned 502"));

        Assert.Equal([("requiredChecks", "Couldn't decide checks"), ("docMap", "No doc named")], page.Strip.Select(static s => (s.Panel, s.Text)));
    }

    [Fact]
    public void The_doc_map_marks_claims_by_others_and_unclaimed_requirements()
    {
        var doc = new ReviewPageDocFact("d", "Doc", "docs/d.md",
        [
            new ReviewPageItemFact("Ph4", "Settings", "phase", Parent: null, ClaimedHere: false, ClaimedBy: [new ReviewPageClaimedBy(2060, Merged: true, "claimed by #2060, merged")]),
            new ReviewPageItemFact("Ph5", "Routes", "phase", Parent: null, ClaimedHere: true, ClaimedBy: []),
            new ReviewPageItemFact("REQ-E4", "Offline", "req", "Ph5", ClaimedHere: false, ClaimedBy: []),
        ]);
        var page = ReviewPageBuilder.Build(Facts(library: NoLibrary() with { Doc = doc, Claims = [new ReviewPageClaim("d#Ph5", "resolved", Error: null)] }));

        var items = Assert.IsType<DocMapData>(Panel(page, "docMap").Data).Doc!.Items;

        Assert.Equal("Ph4,Ph5", string.Join(',', items.Select(static i => i.Key)));
        Assert.Equal("claimed by #2060, merged", Assert.Single(items[0].ClaimedBy).Text);
        var offline = Assert.Single(items[1].Items);
        Assert.Equal(("REQ-E4", true, true), (offline.Key, offline.Unclaimed, offline.FixTask));
    }

    [Fact]
    public void A_session_that_may_not_read_the_claimed_doc_is_told_so()
    {
        var page = ReviewPageBuilder.Build(Facts(library: NoLibrary(hidden: 1)));

        Assert.Equal(("empty", "1 doc not visible to this session"), (Panel(page, "docMap").State, Panel(page, "docMap").Text));
    }

    [Fact]
    public void The_diff_groups_by_the_settings_globs_folds_collapsed_groups_and_notes_renamed_binary_deleted_and_too_large_files()
    {
        var groups = new[] { new PrReviewGroup("Core", ["src/**"]), new PrReviewGroup("Assets", ["assets/**"]) };
        var files = new[]
        {
            File("src/B.cs", PullFileStatus.Renamed, add: 2, del: 2, previous: "src/A.cs", similarity: 91),
            File("src/Old.cs", PullFileStatus.Removed, add: 0, del: 61),
            File("assets/b.png", PullFileStatus.Added, add: 0, del: 0, patch: null),
            File("src/Big.cs", add: 12000, patch: new string('x', (1024 * 1024) + 1)),
            File("src/Huge.cs", add: 5, patch: null),
            File("README.md"),
        };
        var diff = ReviewPageBuilder.Build(Facts(With(groups: groups, collapse: ["assets/**"]), files: files)).Diff;

        Assert.Equal([("Core", false), ("Assets", true), ("Other", false)], diff.Groups.Select(static g => (g.Name, g.Folded)));
        var core = diff.Groups[0].Files;
        Assert.Equal(("Renamed from src/A.cs, 91% the same", 91, null, "renamed"), (core[0].Note, core[0].Similarity, core[0].Omitted, core[0].Status));
        Assert.Equal(("deleted", "Deleted file: 61 lines removed. Contents hidden.", "deleted"), (core[1].Omitted, core[1].Note, core[1].Status));
        Assert.Equal(("tooLarge", "Too large to show here", null), (core[2].Omitted, core[2].Note, core[2].Patch));
        Assert.Equal("forgeOmitted", core[3].Omitted);
        Assert.Equal(("binary", "Binary file, not shown here."), (diff.Groups[1].Files[0].Omitted, diff.Groups[1].Files[0].Note));
        Assert.StartsWith(Url + "/files#diff-", core[0].Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_groups_the_diff_groups_by_the_first_two_path_segments()
    {
        var diff = ReviewPageBuilder.Build(Facts(files: [File("src/Api/A.cs"), File("src/Web/B.cs"), File("src/Api/C.cs"), File("README.md")])).Diff;

        Assert.Equal([("src/Api", 2), ("src/Web", 1), ("Other", 1)], diff.Groups.Select(static g => (g.Name, g.Files.Count)));
    }

    [Fact]
    public void A_lockfile_only_group_starts_folded_by_the_default_collapse_globs()
    {
        var diff = ReviewPageBuilder.Build(Facts(files: [File("pkg/packages.lock.json"), File("src/A.cs")])).Diff;

        Assert.Equal([("pkg", true), ("src", false)], diff.Groups.Select(static g => (g.Name, g.Folded)));
    }

    [Fact]
    public void At_githubs_cap_the_header_says_3000_files_and_the_list_is_what_github_gave()
    {
        var diff = ReviewPageBuilder.Build(Facts(files: [File("src/A.cs")], capped: true)).Diff;

        Assert.Equal((3000, true, "GitHub lists the first 3,000 files; open the rest on GitHub", 1), (diff.FileCount, diff.Capped, diff.CappedText, diff.Groups.Sum(static g => g.Files.Count)));
    }

    [Fact]
    public void A_moving_head_sets_the_banner()
    {
        Assert.Equal("The PR is moving; reload when it settles.", ReviewPageBuilder.Build(Facts(moving: true)).Moving?.Text);
        Assert.Null(ReviewPageBuilder.Build(Facts()).Moving);
    }

    [Fact]
    public void Threads_become_the_conversation_and_runs_become_the_ci_panel()
    {
        var thread = new PullThread("thr_1", PullCommentKind.Thread, "kim-dev", "MEMBER", IsBot: false, Resolved: false, Outdated: false, "src/A.cs", "Cap at 5?", Markers: [], _at, Line: 12, EditedAt: null, Url: null);
        var page = ReviewPageBuilder.Build(Facts(threads: [thread], runs: [new PullCheckRun("ui", "actions", "failure", Head), new PullCheckRun("lint", "actions", "pending", Head)]));

        var first = Assert.Single(Assert.IsType<ConversationData>(Panel(page, "conversation").Data).Threads);
        var runs = Assert.IsType<CiData>(Panel(page, "ci").Data).Runs;

        Assert.Equal(("thr_1", "src/A.cs", 12, "kim-dev"), (first.Key, first.Path, first.Line, Assert.Single(first.Comments).Author));
        Assert.Equal([("ui", "completed", "failure"), ("lint", "in_progress", null)], runs.Select(static r => (r.Name, r.Status, r.Conclusion)));
    }

    [Fact]
    public void An_evidence_heading_in_the_body_adds_the_body_row_and_a_risks_section_adds_its_first_line()
    {
        var page = ReviewPageBuilder.Build(Facts(body: "proof: ran it\n\n## Evidence\nSee below.\n\n### Risks\n\nThe migration rewrites retry rows.\nMore."));

        Assert.Equal("prBody", Assert.Single(Assert.IsType<EvidenceData>(Panel(page, "evidence").Data).Items).Kind);
        var hit = Assert.Single(Assert.IsType<RiskData>(Panel(page, "risk").Data).Hits);
        Assert.Equal(("PR body", "The migration rewrites retry rows.", "body"), (hit.Path, hit.Why, hit.Source));
        Assert.Empty(Assert.IsType<EvidenceData>(Panel(ReviewPageBuilder.Build(Facts(body: "## Evidenced elsewhere")), "evidence").Data).Items);
    }

    [Fact]
    public void A_risks_section_with_windows_line_endings_gives_the_first_line_not_an_empty_row()
    {
        var page = ReviewPageBuilder.Build(Facts(body: "intro\r\n\r\n## Risks\r\n\r\n- db lock\r\nMore."));

        var hit = Assert.Single(Assert.IsType<RiskData>(Panel(page, "risk").Data).Hits);
        Assert.Equal(("PR body", "- db lock", "body"), (hit.Path, hit.Why, hit.Source));
    }

    [Fact]
    public void Unread_files_make_the_risk_panel_an_error_with_no_strip_part_not_an_all_clear()
    {
        var page = ReviewPageBuilder.Build(Facts(filesError: "github returned 502"));

        var risk = Panel(page, "risk");
        Assert.Equal(("error", "Couldn't read the files: github returned 502"), (risk.State, risk.Text));
        Assert.DoesNotContain(page.Strip, static p => p.Panel == "risk");
    }
}
