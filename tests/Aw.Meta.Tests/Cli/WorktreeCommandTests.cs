using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>worktree-list and worktree-sweep end to end: real git (a bare origin, the shared checkout, worktrees), real process scan, a stub gh.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WorktreeCommandTests : IDisposable
{
    private readonly WorktreeLab _lab = new();
    private readonly Dictionary<string, string> _trees = new(StringComparer.Ordinal);

    // The box: ten worktrees that each fail (or pass) a different rule.
    private async Task Box()
    {
        await _lab.Init();
        _ = await _lab.Branch("merged-clean", pr: 1);
        _ = await _lab.Branch("merged-notes", pr: 2);
        _ = await _lab.Branch("merged-untracked-obj", pr: 3);
        _ = await _lab.Branch("merged-busy", pr: 4);
        _ = await _lab.Branch("merged-locked", pr: 5);
        _ = await _lab.Branch("outside-root", pr: 6);
        _ = await _lab.Branch("unmerged-open", pr: 7);
        _ = await _lab.Branch("unmerged-nopr");
        _ = await _lab.Branch("closed-pushed", pr: 8);
        _ = await _lab.Branch("closed-unpushed", pr: 9);
        foreach (var merged in new[] { "merged-clean", "merged-notes", "merged-untracked-obj", "merged-busy", "merged-locked", "outside-root" })
        {
            WorktreeLab.Git(_lab.Repo, "merge", "--quiet", "--no-ff", "--no-edit", merged);
        }

        WorktreeLab.Git(_lab.Repo, "push", "--quiet", "origin", "main:refs/heads/main");
        foreach (var name in new[] { "merged-clean", "merged-notes", "merged-untracked-obj", "merged-busy", "merged-locked", "unmerged-open", "unmerged-nopr", "closed-pushed", "closed-unpushed" })
        {
            _trees[name] = _lab.Tree(name);
        }

        _trees["outside-root"] = _lab.Tree("outside-root", _lab.Cli.File("elsewhere"));
        await File.WriteAllTextAsync(Path.Combine(_trees["merged-notes"], "NOTES.md"), "remember", TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.Combine(_trees["merged-untracked-obj"], "obj"));
        await File.WriteAllTextAsync(Path.Combine(_trees["merged-untracked-obj"], "obj", "x.dll"), "bits", TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.Combine(_trees["merged-clean"], "bin"));   // ignored by .gitignore: build output that git itself ignores
        await File.WriteAllTextAsync(Path.Combine(_trees["merged-clean"], "bin", "a.dll"), "bits", TestContext.Current.CancellationToken);
        await WorktreeLab.CommitLocally(_trees["closed-unpushed"], "extra.txt");
        WorktreeLab.Git(_lab.Repo, "worktree", "lock", "--reason", "on a usb disk", _trees["merged-locked"]);
        _lab.StartSleeper(_trees["merged-busy"]);
        await _lab.Gh(
            (1, "MERGED", "merged-clean"), (2, "MERGED", "merged-notes"), (3, "MERGED", "merged-untracked-obj"), (4, "MERGED", "merged-busy"),
            (5, "MERGED", "merged-locked"), (6, "MERGED", "outside-root"), (7, "OPEN", "unmerged-open"), (8, "CLOSED", "closed-pushed"), (9, "CLOSED", "closed-unpushed"));
        await _lab.Team(buildOutputs: ["bin", "obj"]);
        _lab.Cli.Clock.SetUtcNow(RealTime.NowOffset.AddMinutes(1));   // a sweep records its start after the trees were made, so the next one sees them unchanged
    }

    private static Dictionary<string, JsonElement> ByBranch(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("worktrees").EnumerateArray().ToDictionary(
            w => w.TryGetProperty("branch", out var branch) && branch.ValueKind == JsonValueKind.String ? branch.GetString()! : "(detached)",
            w => w.Clone(),
            StringComparer.Ordinal);
    }

    private static string Reasons(JsonElement worktree) => string.Join(" | ", worktree.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()));

    [Fact, Trait("Lane", "integration")]
    public async Task Worktree_list_gives_every_worktree_a_verdict_and_every_reason()
    {
        await Box();

        var list = await _lab.Cli.Run("worktree-list", "--json");

        Assert.Equal(0, list.Exit);
        var report = ByBranch(list.Out);
        Assert.Equal("removable", report["merged-clean"].GetProperty("decision").GetString(), ignoreCase: true);
        Assert.Equal("removable", report["closed-pushed"].GetProperty("decision").GetString(), ignoreCase: true);
        Assert.Contains("NOTES.md", Reasons(report["merged-notes"]), StringComparison.Ordinal);
        Assert.Contains("not merged", Reasons(report["unmerged-nopr"]), StringComparison.Ordinal);
        Assert.Contains("open", Reasons(report["unmerged-open"]), StringComparison.Ordinal);
        Assert.Contains("sleep", Reasons(report["merged-busy"]), StringComparison.Ordinal);
        Assert.Contains("locked", Reasons(report["merged-locked"]), StringComparison.Ordinal);
        Assert.Contains("1 commit", Reasons(report["closed-unpushed"]), StringComparison.Ordinal);
        Assert.Contains("allowed", Reasons(report["outside-root"]), StringComparison.Ordinal);
        Assert.Contains("main worktree", Reasons(report["main"]), StringComparison.Ordinal);
        Assert.Equal(7, report["unmerged-open"].GetProperty("pr").GetInt32());
        Assert.Equal("Open", report["unmerged-open"].GetProperty("prState").GetString(), ignoreCase: true);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Worktree_list_prints_a_line_per_worktree_with_its_reasons_underneath()
    {
        await Box();

        var list = await _lab.Cli.Run("worktree-list");

        Assert.Equal(0, list.Exit);
        Assert.Matches(@"REMOVABLE\s+\S*merged-clean\b", list.Out);
        Assert.Matches(@"KEPT\s+\S*merged-notes\b", list.Out);
        Assert.Contains("NOTES.md", list.Out, StringComparison.Ordinal);
        Assert.Contains("removable", list.Out, StringComparison.Ordinal);   // the summary line
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Worktree_list_can_skip_the_fetch_and_changes_nothing_on_disk()
    {
        await Box();
        var before = WorktreeLab.GitOut(_lab.Repo, "worktree", "list", "--porcelain");

        var list = await _lab.Cli.Run("worktree-list", "--no-fetch", "--json");

        Assert.Equal(0, list.Exit);
        Assert.Equal(before, WorktreeLab.GitOut(_lab.Repo, "worktree", "list", "--porcelain"));
        Assert.All(_trees.Values, tree => Assert.True(Directory.Exists(tree), tree));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_dry_run_removes_nothing_and_says_what_it_would_remove()
    {
        await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--dry-run");

        Assert.Equal(0, sweep.Exit);
        Assert.Matches(@"WOULD REMOVE\s+\S*merged-clean\b", sweep.Out);
        Assert.All(_trees.Values, tree => Assert.True(Directory.Exists(tree), tree));
        Assert.False(File.Exists(Path.Combine(_lab.Cli.File("meta"), "log.md")) && (await File.ReadAllTextAsync(Path.Combine(_lab.Cli.File("meta"), "log.md"), TestContext.Current.CancellationToken)).Contains("worktrees", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_sweep_removes_only_the_removable_worktrees_and_never_a_branch()
    {
        await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.Equal(1, sweep.Exit);   // git itself refuses the untracked obj/: nothing is forced, and a refusal asks for attention
        Assert.False(Directory.Exists(_trees["merged-clean"]));
        Assert.False(Directory.Exists(_trees["closed-pushed"]));
        foreach (var kept in new[] { "merged-notes", "merged-untracked-obj", "merged-busy", "merged-locked", "outside-root", "unmerged-open", "unmerged-nopr", "closed-unpushed" })
        {
            Assert.True(Directory.Exists(_trees[kept]), kept);
        }

        Assert.Matches(@"REMOVED\s+\S*merged-clean\b", sweep.Out);
        Assert.Matches(@"KEPT\s+\S*merged-notes\b", sweep.Out);
        Assert.Contains("refused", sweep.Out, StringComparison.Ordinal);   // the untracked obj/
        Assert.Contains("merged-clean", WorktreeLab.GitOut(_lab.Repo, "branch", "--list"), StringComparison.Ordinal);   // branches are never deleted
        Assert.DoesNotContain("merged-clean", WorktreeLab.GitOut(_lab.Repo, "worktree", "list"), StringComparison.Ordinal);
        Assert.Contains("worktrees sweep", await File.ReadAllTextAsync(Path.Combine(_lab.Cli.File("meta"), "log.md"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_second_sweep_has_nothing_left_to_remove()
    {
        await Box();
        _ = await _lab.Cli.Run("worktree-sweep");

        var again = await _lab.Cli.Run("worktree-sweep", "--json");

        using var document = JsonDocument.Parse(again.Out);
        // The first sweep keeps a no-PR branch (no previous sweep to compare with); the second keeps it too (no PR, without --include-no-pr), so nothing but a no-PR branch could show up here.
        Assert.All(document.RootElement.GetProperty("removed").EnumerateArray(), removed => Assert.Contains("unmerged-nopr", removed.ToString(), StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_worktree_in_use_is_kept_and_removed_by_the_next_sweep_once_it_is_idle()
    {
        await Box();
        _ = await _lab.Cli.Run("worktree-sweep");
        Assert.True(Directory.Exists(_trees["merged-busy"]));

        _lab.StopSleepers();
        var later = await _lab.Cli.Run("worktree-sweep");

        Assert.False(Directory.Exists(_trees["merged-busy"]), later.Out);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Sweeping_one_pull_request_touches_only_its_branch()
    {
        await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--pr", "8");

        Assert.Equal(0, sweep.Exit);
        Assert.False(Directory.Exists(_trees["closed-pushed"]));
        Assert.True(Directory.Exists(_trees["merged-clean"]));
        Assert.DoesNotContain("merged-notes", sweep.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task When_github_cannot_be_read_nothing_is_removed()
    {
        await Box();
        _lab.Cli.SetEnv("AW_GH", _lab.Cli.File("no-such-gh"));

        var sweep = await _lab.Cli.Run("worktree-sweep");

        Assert.Equal(2, sweep.Exit);
        Assert.StartsWith("AW-META-", sweep.Err, StringComparison.Ordinal);
        Assert.All(_trees.Values, tree => Assert.True(Directory.Exists(tree), tree));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bad_pull_request_number_is_a_usage_error()
    {
        await Box();

        var sweep = await _lab.Cli.Run("worktree-sweep", "--pr", "seven");

        Assert.Equal(2, sweep.Exit);
        Assert.StartsWith("AW-META-001: ", sweep.Err, StringComparison.Ordinal);
    }

    public void Dispose() => _lab.Dispose();
}
