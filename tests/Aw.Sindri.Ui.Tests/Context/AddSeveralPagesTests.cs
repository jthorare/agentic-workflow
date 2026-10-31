using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2c: the "Add several pages" sheet reads a pasted list as one row per page and writes every ticked row in one profile write (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class AddSeveralPagesTests
{
    private const string Hash = "hash-preview";

    private static PagesPreviewBlock Block(int number, string name, int? sameAs = null, string? reason = null, bool ticked = true, PagesPreviewMatch? match = null, IReadOnlyList<PagesPreviewAction>? actions = null) =>
        new(number, number, name, match, Replaces: null, sameAs, ticked && reason is null, reason, actions ?? [], [],
            ticked && reason is null ? new PagesPreviewPage(Id: null, name, "web", Surface: null, [new PagesPreviewPageAction("Submit", Action: null, ["self-scheduling"])]) : null);

    private static (AddSeveralPagesViewModel Sheet, FakePageWrites Writes) Sheet(params PagesPreviewBlock[] blocks) =>
        Sheet(new PagesPreviewRead(new PagesPreview("v41", Hash, blocks)));

    private static (AddSeveralPagesViewModel Sheet, FakePageWrites Writes) Sheet(PagesPreviewRead answer)
    {
        var writes = new FakePageWrites { PreviewAnswer = answer };
        return (new AddSeveralPagesViewModel(writes, repo: null), writes);
    }

    [Fact]
    public async Task Two_blocks_naming_the_same_page_flag_the_second()
    {
        var (sheet, _) = Sheet(Block(1, "Schedule week"), Block(2, "Schedule week", sameAs: 1, reason: "Not added: it names the same page as block 1"));
        sheet.Text = "Schedule week\nSubmit\n\nSchedule week\nCopy";

        await sheet.ReadList(TestContext.Current.CancellationToken);

        Expect.Equal(2, sheet.Rows.Count);
        Expect.Equal(string.Empty, sheet.Rows[0].SameAsText);
        Expect.Equal("Same page as block 1", sheet.Rows[1].SameAsText);
        Expect.True(sheet.Rows[0].Ticked, "the first block starts ticked");
        Expect.True(!sheet.Rows[1].Ticked, "the second block starts unticked");
    }
}
