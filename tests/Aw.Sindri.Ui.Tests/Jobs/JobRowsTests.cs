using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The Jobs table's words (F3 jobs spec §1): kinds by plain names, the key beside them, sentence-case states, "1 round".</summary>
[Lane(TestLane.Unit)]
public sealed class JobRowsTests
{
    private readonly JobsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("SyncSources", "Sync sources")]
    [InlineData("RefreshContextMap", "Refresh the context map")]
    [InlineData("BuildContextMap", "Build the context map")]
    [InlineData("RunService", "Run a service")]
    [InlineData("ApproveAllRecipes", "Approve all workflows")]
    [InlineData("AddRepo", "Add a repository")]
    [InlineData("RegisterMcp", "Connect agents")]
    public void A_known_kind_reads_by_its_plain_name(string kind, string expected) => Assert.Equal(expected, Labels.JobKind(kind));

    [Fact]
    public async Task An_unknown_kind_reads_as_a_humanised_served_name_and_the_key_stays_beside_it()
    {
        var page = await _f.Shown(Token);

        var row = Assert.Single(page.Rows);

        Assert.Equal(("Echo job", "echo"), (row.TitleText, row.KeyText));
    }

    [Theory]
    [InlineData(0, "0 rounds")]
    [InlineData(1, "1 round")]
    [InlineData(3, "3 rounds")]
    public async Task Rounds_are_counted_in_words(int rounds, string expected)
    {
        var list = Golden("jobs", "running");
        list["jobs"]![0]!["rounds"] = rounds;
        _f.ServeList(list);

        Assert.Equal(expected, Assert.Single((await _f.Shown(Token)).Rows).RoundsText);
    }

    [Theory]
    [InlineData("running", "Running")]
    [InlineData("succeeded", "Done")]
    [InlineData("failed", "Failed")]
    [InlineData("cancelled", "Cancelled")]
    public void States_read_in_sentence_case(string state, string word) => Assert.Equal(word, Labels.JobChip(state).Word);
}
