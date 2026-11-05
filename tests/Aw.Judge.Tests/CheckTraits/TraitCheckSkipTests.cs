using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// What trait-check never judges: files .gitattributes marks linguist-generated (any mode), and, in edit and stop mode,
// a working tree in the middle of a merge, rebase, cherry-pick or revert, whose conflicted files hold someone else's
// code. The tests run the service over a temp git repo and a stub Jev that calls every asked trait broken.
public sealed class TraitCheckSkipTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private const string Generated = "src/gen/schema.ts";

    [Theory, Trait("Lane", "unit")]
    [InlineData("linguist-generated=true")]
    [InlineData("linguist-generated")]
    public async Task CheckTraits_edit_skips_a_file_gitattributes_marks_linguist_generated(string attribute)
    {
        using var repo = new TraitRepo();
        repo.Write(".gitattributes", $"src/gen/** {attribute}\n");
        repo.Write(Generated, "// TODO: generated\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit(Generated).Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), result.Value);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_stop_judges_only_the_changed_files_that_are_not_generated()
    {
        using var repo = new TraitRepo();
        repo.Write(".gitattributes", "src/gen/** linguist-generated=true\n");
        repo.Write(Generated, "// TODO: generated\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        Assert.Equal("src/app.ts", Assert.Single(result.Value.Findings.Select(finding => finding.File).Distinct(StringComparer.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_scan_never_judges_a_generated_file()
    {
        using var repo = new TraitRepo();
        repo.Write(".gitattributes", "src/gen/** linguist-generated=true\n");
        repo.Write(Generated, "// TODO: generated\n");
        _ = repo.Git("add", "-A");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Scan().Build(), _ct);

        Assert.Equal("src/app.ts", Assert.Single(result.Value.Findings.Select(finding => finding.File).Distinct(StringComparer.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_and_stop_skip_while_a_merge_conflict_is_being_resolved()
    {
        using var repo = new TraitRepo();
        Conflict(repo);
        repo.Write("src/app.ts", "export const a = 3;\n// TODO: resolve\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var edit = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        var stop = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), edit.Value);
        Assert.Equal(new CheckTraitsResponse([]), stop.Value);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_judges_again_once_the_merge_is_committed()
    {
        using var repo = new TraitRepo();
        Conflict(repo);
        repo.Write("src/app.ts", "export const a = 3;\n");
        _ = repo.Git("add", "-A");
        _ = Commit(repo, "merged");
        repo.Write("src/app.ts", "export const a = 3;\n// TODO: after\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        _ = Assert.Single(result.Value.Findings);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("MERGE_HEAD")]
    [InlineData("CHERRY_PICK_HEAD")]
    [InlineData("REVERT_HEAD")]
    [InlineData("rebase-merge/head-name")]
    [InlineData("rebase-apply/next")]
    public async Task CheckTraits_edit_skips_while_git_marks_an_operation_in_progress_and_scan_does_not(string marker)
    {
        using var repo = new TraitRepo();
        var path = repo.Git("rev-parse", "--path-format=absolute", "--git-path", marker).Trim();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a git path always has a parent directory
        await File.WriteAllTextAsync(path, "refs/heads/main\n", _ct);
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var edit = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        var scan = await harness.Service.Handle(A.CheckTraitsRequest().Scan().Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), edit.Value);
        Assert.NotEmpty(scan.Value.Findings);
    }

    // Leaves the repo mid-merge: 'other' and 'main' both changed src/app.ts, and the merge stopped on the conflict.
    private static void Conflict(TraitRepo repo)
    {
        _ = repo.Git("checkout", "-q", "--", "src/app.ts");
        _ = repo.Git("checkout", "-q", "-b", "other");
        repo.Write("src/app.ts", "export const a = 2;\n");
        _ = Commit(repo, "other", "-a");
        _ = repo.Git("checkout", "-q", "main");
        repo.Write("src/app.ts", "export const a = 3;\n");
        _ = Commit(repo, "main", "-a");
        _ = Assert.Throws<InvalidOperationException>(() => repo.Git("-c", "user.name=t", "-c", "user.email=t@example.com", "merge", "-q", "other"));
    }

    private static string Commit(TraitRepo repo, string message, params string[] flags) =>
        repo.Git(["-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", "commit", "-q", .. flags, "-m", message]);
}
