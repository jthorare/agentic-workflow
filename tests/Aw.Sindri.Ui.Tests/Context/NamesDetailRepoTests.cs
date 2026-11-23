using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#3861: with several repos in scope, the coverage sentence names the repo whose code index is not built, so the reader knows which one to build.</summary>
[Lane(TestLane.Unit)]
public sealed class NamesDetailRepoTests
{
    private static SymbolsView NotBuilt(params string[] repos) => new("unavailable", "index not built", [], Repos: repos.Length == 0 ? null : repos);

    [Fact]
    public void One_repo_with_no_index_is_named()
    {
        Assert.Equal("No code index for aw-split-panes yet.", NamesDetail.Sentence(NotBuilt("aw-split-panes")));
    }

    [Fact]
    public void Several_repos_with_no_index_are_all_named()
    {
        Assert.Equal("No code index for api, docs and web yet.", NamesDetail.Sentence(NotBuilt("api", "docs", "web")));
    }

    [Fact]
    public void An_answer_that_names_no_repo_keeps_the_old_words()
    {
        Assert.Equal(NamesDetail.Sentence(new SymbolsView("unavailable", "index not built", [])), NamesDetail.Sentence(NotBuilt()));
        Assert.DoesNotContain("No code index for", NamesDetail.Sentence(NotBuilt()), StringComparison.Ordinal);
    }
}
