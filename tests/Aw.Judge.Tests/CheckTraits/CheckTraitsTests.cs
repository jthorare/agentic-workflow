using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.Traits;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// trait-check (cutover spec §5.2, unit 6.2): the repo's traits (<repo>/.agents/traits/*.yaml, overlaid by
// <state>/traits/<repo-name>/*.yaml) judged against a diff or whole files in batched Jev requests. The tests run the
// service over a temp git repo and a stub Jev.
public sealed class CheckTraitsTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_edit_reports_a_block_trait_the_diff_breaks()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.True(result.IsSuccess);
        var finding = Assert.Single(result.Value.Findings);
        Assert.Equal(("no-todo", "block", "src/app.ts"), (finding.Trait, finding.Severity, finding.File));
        Assert.Equal(("Open an issue and link it.", ".agents/traits/no-todo.yaml"), (finding.Guidance, finding.Details));
        _ = Assert.Single(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_skips_a_file_no_trait_path_matches_without_asking_jev()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("docs/readme.md").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Findings);
        Assert.False(result.Value.JevUnavailable);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_with_jev_excluded_by_the_allowlist_is_jev_unavailable_and_reports_nothing()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Allowing(ProviderName.Rules).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.JevUnavailable);
        Assert.Equal("excluded", result.Value.Reason);
        Assert.Empty(result.Value.Findings);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_with_jev_unreachable_is_jev_unavailable_and_logs_why()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, FakeBatchClassifier.Unavailable());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.True(result.Value.JevUnavailable);
        Assert.Equal("no-api-key", result.Value.Reason);
        Assert.Contains(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "jev-unavailable" && entry["reason"]?.GetValue<string>() == "no-api-key");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_in_a_repo_without_traits_skips_without_asking_jev()
    {
        using var repo = new TraitRepo();
        File.Delete(Path.Combine(repo.Root, ".agents", "traits", "no-todo.yaml"));
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), result.Value);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_stop_judges_the_branch_diff_against_origin_main()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Value.Findings);
        Assert.Contains(harness.Jev.Asked, batch => batch.State?.ToJsonString().Contains("origin/main", StringComparison.Ordinal) == true);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_stop_reports_only_block_traits()
    {
        using var repo = new TraitRepo();
        repo.Write(".agents/traits/no-todo.yaml", TraitRepo.TraitText.Replace("severity: block", "severity: warn", StringComparison.Ordinal));
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var stop = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);
        var edit = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.Empty(stop.Value.Findings);
        Assert.Equal("warn", Assert.Single(edit.Value.Findings).Severity);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_reports_a_finding_only_from_the_probability_the_mode_asks_for()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating(p: 0.7));

        var edit = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);
        var stop = await harness.Service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        _ = Assert.Single(edit.Value.Findings);
        Assert.Empty(stop.Value.Findings);
        Assert.Contains(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "assessment" && entry["reported"]?.GetValue<bool>() == false);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_scan_reads_whole_files_and_reports_each_finding_with_its_file()
    {
        using var repo = new TraitRepo();
        repo.Write("src/other.ts", "export const b = 2;\n");
        _ = repo.Git("add", "-A");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Scan("src").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["src/app.ts", "src/other.ts"], result.Value.Findings.Select(finding => finding.File).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains(harness.Jev.Asked, batch => batch.State?.ToJsonString().Contains("export const b = 2;", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(harness.Log.Entries, entry => entry["event"]?.GetValue<string>() == "assessment");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_redacts_a_secret_in_the_diff_before_it_reaches_jev_and_the_log()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        _ = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        var state = Assert.Single(harness.Jev.Asked).State?.ToJsonString();
        Assert.Contains("TODO: remove", state, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken, state, StringComparison.Ordinal);
        Assert.All(harness.Log.Entries, entry => Assert.DoesNotContain(SampleSecrets.GitHubToken, entry.ToJsonString(), StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_honours_a_trait_ignore_file_comment_and_asks_nothing_when_all_are_ignored()
    {
        using var repo = new TraitRepo();
        repo.Write("src/app.ts", "// trait-ignore-file no-todo: generated\nexport const a = 1;\n// TODO: remove\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), result.Value);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_judges_a_file_git_does_not_track_yet_as_a_new_file()
    {
        using var repo = new TraitRepo();
        repo.Write("src/new.ts", "// TODO: later\n");
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/new.ts").Build(), _ct);

        Assert.Equal("src/new.ts", Assert.Single(result.Value.Findings).File);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_lets_an_overlay_trait_replace_the_repo_trait_of_the_same_name()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());
        _ = Directory.CreateDirectory(harness.Overlay);
        await File.WriteAllTextAsync(Path.Combine(harness.Overlay, "no-todo.yaml"), TraitRepo.TraitText.Replace("src/**", "lib/**", StringComparison.Ordinal), _ct);

        var result = await harness.Service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckTraits_outside_a_repo_skips()
    {
        using var repo = new TraitRepo();
        using var harness = new CheckTraitsHarness(repo, CheckTraitsHarness.Violating());
        using var elsewhere = new TempStateDirectory();
        var service = new CheckTraitsService(
            new SystemProcessRunner(Environment.GetEnvironmentVariable),
            new FileTraitStore(elsewhere.Path),
            new FileTraitFiles(elsewhere.Path),
            new JevTraitJudge(harness.Jev, new FakeInputRedactor(), new InMemoryJudgeConfigStore()),
            harness.Log,
            TimeProvider.System);

        var result = await service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        Assert.Equal(new CheckTraitsResponse([]), result.Value);
    }
}
