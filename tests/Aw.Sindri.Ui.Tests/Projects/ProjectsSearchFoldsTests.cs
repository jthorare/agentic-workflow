using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The search count and its no-match state cover every row the search can list on the Projects view, the Finished and Hidden folds included (issue 4124).</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsSearchFoldsTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_match_only_in_Finished_is_counted_and_is_no_empty_state()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SearchText = "relaunch";   // Search relaunch is the golden's finished project

        Assert.Equal(["Search relaunch"], page.Finished.Cards.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal((false, string.Empty, "1 of 5"), (page.IsSearchEmpty, page.SearchEmptyTitle, page.SearchCount));
    }

    [Fact]
    public async Task A_match_only_in_Hidden_is_counted_and_is_no_empty_state()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SearchText = "billing";   // Billing cleanup is the golden's hidden project

        Assert.Equal(["Billing cleanup"], page.Hidden.Cards.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal((false, "1 of 5"), (page.IsSearchEmpty, page.SearchCount));
    }

    [Fact]
    public async Task A_word_nothing_holds_is_the_empty_state_over_every_row_of_the_view()
    {
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SearchText = "xyz";

        Assert.Equal((true, "0 of 5"), (page.IsSearchEmpty, page.SearchCount));
    }
}
