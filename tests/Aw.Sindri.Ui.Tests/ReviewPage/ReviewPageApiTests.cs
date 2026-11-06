using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.ReviewPage;

/// <summary>GET /reviews/{owner}/{repo}/{number} read through the app's own records (review page spec §6.2, §9): the goldens' fields, and each refusal's code.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPageApiTests
{
    private readonly ReviewPageFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<Result<Ui.Api.Reviews.ReviewPage>> Read() => await _f.Server.ReadReviewPage(ReviewPageFixture.Url, ReviewPageFixture.Repo, ReviewPageFixture.Number, Token);

    [Fact]
    public async Task The_full_golden_reads_the_header_the_strip_the_seven_panels_and_the_diff()
    {
        var page = (await Read()).Value;

        Assert.Equal(("acme/api", 2071, "Add retry budget to the sync worker", "sam-dev", false, "a1b2c3d", "feat/retry-budget"), (page.Pr.Repo, page.Pr.Number, page.Pr.Title, page.Pr.Author, page.Pr.Yours, page.Pr.Head.Sha7, page.Pr.Head.Branch));
        Assert.Equal(["ENG-12"], page.Pr.Issues.Select(i => i.Key), StringComparer.Ordinal);
        Assert.Equal(("sindri", "review.projects[0]"), (page.Settings!.Name, page.Settings.From));
        Assert.Equal(["requiredChecks", "docMap", "risk", "ci"], page.Strip.Select(s => s.Panel), StringComparer.Ordinal);
        Assert.Equal(["requiredChecks", "docMap", "testPlan", "ci", "evidence", "risk", "conversation"], page.Panels.Select(p => p.Id), StringComparer.Ordinal);
        Assert.Equal(5, page.Panels[0].Data.Checks!.Count);
        Assert.Equal(["Ph4", "Ph5"], page.Panels[1].Data.Doc!.Items.Select(i => i.Id), StringComparer.Ordinal);
        Assert.Equal((2, 4), (page.Diff.Groups.Count, page.Diff.FileCount));
    }

    [Fact]
    public async Task The_capped_golden_carries_the_cap_words_and_a_too_large_file_has_no_patch()
    {
        _f.Serve("capped");

        var diff = (await Read()).Value.Diff;

        var file = diff.Groups.SelectMany(g => g.Files).Single();
        Assert.Equal((true, 3000, "GitHub lists the first 3,000 files; open the rest on GitHub"), (diff.Capped, diff.FileCount, diff.CappedText));
        Assert.Equal((null, "tooLarge", "Too large to show here"), (file.Patch, file.Omitted, file.Note));
    }

    [Theory]
    [InlineData("not-found", "SND-REVIEW-404", "PR not found")]
    [InlineData("not-synced", "SND-REVIEW-004", "Sindri doesn't sync acme/other. Add it to `context.sources.github.repos`.")]
    [InlineData("no-login", "SND-REVIEW-007", "Set your GitHub login on the Reviews tab first.")]
    public async Task A_refusal_golden_reads_as_its_code_and_detail(string scenario, string code, string detail)
    {
        _f.Refuse(scenario);

        var read = await Read();

        Assert.Equal((code, detail), (read.Error.Code, read.Error.Message));
    }
}
