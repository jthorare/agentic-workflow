using System.Text.Json.Nodes;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// The repo a check judges is the edited file's, not the session's: a session started in repo A that edits repo B's
// files by absolute path gets B's traits, and its Stop judges B's branch too (the repos an edit saw are kept per
// session under the judge's state). A repo or a file the check passes over leaves a 'skipped' line in the log with
// why. The tests run the service over temp git repos and a stub Jev that calls every asked trait broken.
public sealed class TraitCheckRepoTests : IDisposable
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly TraitRepo _home = new();
    private readonly TraitRepo _other = new();

    public TraitCheckRepoTests()
    {
        File.Delete(Path.Combine(_home.Root, ".agents", "traits", "no-todo.yaml"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_of_a_file_in_another_repo_judges_it_against_that_repos_traits()
    {
        using var harness = new CheckTraitsHarness(_other, CheckTraitsHarness.Violating(), workingDirectory: _home.Root);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit(Path.Combine(_other.Root, "src", "app.ts")).Build(), _ct);

        var finding = Assert.Single(result.Value.Findings);
        Assert.Equal(("no-todo", Top(_other) + "/src/app.ts"), (finding.Trait, finding.File));
        Assert.Equal(Path.Combine(Top(_other), ".agents", "traits", "no-todo.yaml"), finding.Details);
        Assert.Contains(harness.Log.Entries, entry => Event(entry) == "assessment" && Text(entry, "repo") == Top(_other) && Text(entry, "path") == "src/app.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_across_two_repos_judges_each_file_against_its_own_repos_traits()
    {
        _home.Write(".agents/traits/home-rule.yaml", TraitRepo.TraitText.Replace("name: no-todo", "name: home-rule", StringComparison.Ordinal));
        using var harness = new CheckTraitsHarness(_other, CheckTraitsHarness.Violating(), workingDirectory: _home.Root);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts", Path.Combine(_other.Root, "src", "app.ts")).Build(), _ct);

        Assert.Equal(
            [("home-rule", "src/app.ts"), ("no-todo", Top(_other) + "/src/app.ts")],
            result.Value.Findings.Select(finding => (finding.Trait, finding.File)).Order());
        Assert.Equal(2, harness.Jev.Asked.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_stop_also_judges_a_repo_the_session_edited_files_in()
    {
        using var harness = new CheckTraitsHarness(_other, CheckTraitsHarness.Violating(), workingDirectory: _home.Root);
        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit(Path.Combine(_other.Root, "src", "app.ts")).InSession("s1").Build(), _ct);

        var stop = await harness.Service.Handle(A.CheckTraitsRequest().Stop().InSession("s1").Build(), _ct);
        var otherSession = await harness.Service.Handle(A.CheckTraitsRequest().Stop().InSession("s2").Build(), _ct);

        Assert.Equal(Top(_other) + "/src/app.ts", Assert.Single(stop.Value.Findings).File);
        Assert.Empty(otherSession.Value.Findings);
        Assert.Contains(harness.Log.Entries, entry => Event(entry) == "skipped" && Text(entry, "mode") == "stop" && Text(entry, "repo") == Top(_home) && Text(entry, "reason") == "no-traits");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_ignores_a_file_outside_any_repo_and_logs_it()
    {
        using var elsewhere = new TempStateDirectory();
        var loose = Path.Combine(elsewhere.Path, "notes.ts");
        await File.WriteAllTextAsync(loose, "// TODO: loose\n", _ct);
        using var harness = new CheckTraitsHarness(_other, CheckTraitsHarness.Violating(), workingDirectory: _home.Root);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit(loose).InSession("s1").Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), result.Value);
        Assert.Empty(harness.Jev.Asked);
        Assert.Contains(harness.Log.Entries, entry => Event(entry) == "skipped" && Text(entry, "reason") == "not-in-a-repo" && Text(entry, "path") == loose);
        Assert.Empty(harness.Sessions.Repos(SessionId.From("s1")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_of_a_path_whose_directory_is_gone_finds_the_repo_from_the_nearest_parent()
    {
        using var harness = new CheckTraitsHarness(_other, CheckTraitsHarness.Violating(), workingDirectory: _home.Root);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit(Path.Combine(_other.Root, "src", "gone", "deep", "x.ts")).InSession("s1").Build(), _ct);

        Assert.Empty(result.Value.Findings);
        Assert.Equal([Top(_other)], harness.Sessions.Repos(SessionId.From("s1")));
        Assert.Contains(harness.Log.Entries, entry => Event(entry) == "skipped" && Text(entry, "repo") == Top(_other) && Text(entry, "reason") == "no-change-in-scope");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_logs_a_skip_with_its_reason_for_a_repo_without_traits_and_an_edit_no_trait_covers()
    {
        using var harness = new CheckTraitsHarness(_other, CheckTraitsHarness.Violating(), workingDirectory: _home.Root);

        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit(Path.Combine(_other.Root, "docs", "readme.md")).Build(), _ct);

        Assert.Equal(
            [(Top(_home), "no-traits"), (Top(_other), "no-file-in-scope")],
            harness.Log.Entries.Where(entry => Event(entry) == "skipped" && Text(entry, "mode") == "edit").Select(entry => (Text(entry, "repo"), Text(entry, "reason"))));
        Assert.Empty(harness.Jev.Asked);
    }

    public void Dispose()
    {
        _home.Dispose();
        _other.Dispose();
    }

    // git names a repo by its real path (a temp directory under /var is /private/var on macOS), and so does the check.
    private static string Top(TraitRepo repo) => repo.Git("rev-parse", "--show-toplevel").TrimEnd('\n');

    private static string? Event(JsonObject entry) => Text(entry, "event");

    private static string? Text(JsonObject entry, string name) => entry[name]?.GetValue<string>();
}
