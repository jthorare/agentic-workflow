using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's keys (#3677, decision 4): one hint line under the list, and Enter on a file row opens it.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextKeyHintsTests
{
    [Fact]
    public async Task The_concepts_list_hints_arrows_Enter_pin_and_ask_on_one_line()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));

        Assert.True(page.ShowConcepts);
        Assert.Equal(["↑ ↓ move", "Enter open", "p pin", "/ ask", "Esc back"], page.KeyHints.Split(" · "));
        Assert.DoesNotContain('\n', page.KeyHints);
    }

    [Fact]
    public async Task The_files_list_hints_Enter_open_and_has_no_pin_key()
    {
        var page = await ConceptPage.Made(new FakeSindriApi());
        page.ChooseView(files: true);

        Assert.Contains("Enter open", page.KeyHints.Split(" · "), StringComparer.Ordinal);
        Assert.DoesNotContain("p pin", page.KeyHints.Split(" · "), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Enter_on_a_file_row_opens_the_map_around_that_file()
    {
        var page = await ConceptPage.Made(new FakeSindriApi());
        page.ChooseView(files: true);
        var file = new FileRowViewModel(new FileMapEntry("x.cs", "src/x.cs", "file", "confirmed", Files: 0, [], Intent: null));

        await page.FileMap.Open(file, TestContext.Current.CancellationToken);

        Assert.Equal((true, "code:src/x.cs"), (page.ShowMap, page.AnchorText));
    }
}
