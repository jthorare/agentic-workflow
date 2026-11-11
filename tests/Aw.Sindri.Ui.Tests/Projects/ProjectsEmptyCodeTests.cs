using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page branches on the served empty code, never on the wording of the served sentence (#2625).</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsEmptyCodeTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ViewModels.Projects.ProjectsPageViewModel> ShownWith(string? empty, string? code)
    {
        var list = Golden("mixed");
        list["empty"] = empty;
        list["emptyCode"] = code;
        list["viewer"] = "Ada";
        _f.Serve(list);
        return await _f.Shown(Token);
    }

    [Fact]
    public async Task A_reworded_key_missing_sentence_with_the_key_missing_code_offers_Connect_Linear_and_reads_last_seen()
    {
        var page = await ShownWith("Your Linear key is missing.", "linear-key-missing");

        Assert.True(page.ShowsConnect);
        Assert.Contains("last seen as", page.Freshness, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_reworded_no_linear_sentence_with_the_linear_missing_code_offers_Connect_Linear()
    {
        var page = await ShownWith("Linear isn't set up.", "linear-missing");

        Assert.True(page.ShowsConnect);
    }

    [Fact]
    public async Task An_empty_sentence_with_no_code_does_not_offer_Connect_Linear()
    {
        var page = await ShownWith("Linear is still syncing. Projects you lead show up when it's done.", code: null);

        Assert.False(page.ShowsConnect);
    }
}
