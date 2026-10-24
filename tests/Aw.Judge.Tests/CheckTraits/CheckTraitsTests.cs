#pragma warning disable xUnit1004 // red tests, written ahead of the cutover unit that makes them pass (spec §12 P0)
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// trait-check (cutover spec §5.2, unit 6.2): the repo's traits (<repo>/.agents/traits/*.yaml, overlaid by
// <state>/traits/<repo-name>/*.yaml) judged against a diff or whole files in batched Jev requests. Red until 6.2: the stub
// takes no ports, so each test builds the fakes the real service will be given.
public sealed class CheckTraitsTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "unit")]
    public async Task CheckTraits_edit_reports_a_block_trait_the_diff_breaks()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new CheckTraitsService();

        var result = await service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.True(result.IsSuccess);
        var finding = Assert.Single(result.Value.Findings);
        Assert.Equal(("block", "src/app.ts"), (finding.Severity, finding.File));
        _ = Assert.Single(jev.Asked);
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "unit")]
    public async Task CheckTraits_skips_a_file_no_trait_path_matches_without_asking_jev()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new CheckTraitsService();

        var result = await service.Handle(A.CheckTraitsRequest().Edit("docs/readme.md").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Findings);
        Assert.False(result.Value.JevUnavailable);
        Assert.Empty(jev.Asked);
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "unit")]
    public async Task CheckTraits_with_jev_excluded_by_the_allowlist_is_jev_unavailable_and_reports_nothing()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new CheckTraitsService();

        var result = await service.Handle(A.CheckTraitsRequest().Stop().Allowing(ProviderName.Rules).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.JevUnavailable);
        Assert.Empty(result.Value.Findings);
        Assert.Empty(jev.Asked);
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "unit")]
    public async Task CheckTraits_stop_judges_the_branch_diff_against_origin_main()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new CheckTraitsService();

        var result = await service.Handle(A.CheckTraitsRequest().Stop().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Contains(jev.Asked, batch => batch.State?.ToJsonString().Contains("origin/main", StringComparison.Ordinal) == true);
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "unit")]
    public async Task CheckTraits_scan_reads_whole_files_and_reports_each_finding_with_its_file()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new CheckTraitsService();

        var result = await service.Handle(A.CheckTraitsRequest().Scan("src").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Findings, finding => Assert.StartsWith("src/", finding.File, StringComparison.Ordinal));
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "unit")]
    public async Task CheckTraits_redacts_a_secret_in_the_diff_before_it_reaches_jev_and_the_log()
    {
        var jev = FakeBatchClassifier.Unavailable();
        var service = new CheckTraitsService();

        _ = await service.Handle(A.CheckTraitsRequest().Edit("src/app.ts").Build(), _ct);

        Assert.DoesNotContain(SampleSecrets.GitHubToken, Assert.Single(jev.Asked).State?.ToJsonString(), StringComparison.Ordinal);
    }
}
