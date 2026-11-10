using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The words of a workflow row (#2550): the short id leads the second line when it differs from the title, the person's own actor reads "you", a first revision says nothing, and a reason reads as a sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsRowWordsTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_second_line_leads_with_the_short_id_when_it_differs_from_the_title()
    {
        var page = await _f.Shown(Token);

        var row = page.Rows[0];   // "morning-check" is titled "Morning check"
        Assert.Equal(("morning-check", true), (row.IdText, row.HasIdText));
    }

    [Fact]
    public async Task A_title_that_is_the_short_id_shows_no_id_line()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["name"] = "morning-check";
        _f.ServeList(list);

        var page = await _f.Shown(Token);

        Assert.Equal((string.Empty, false), (page.Rows[0].IdText, page.Rows[0].HasIdText));
    }

    [Theory]
    [InlineData("human:ui", "Changed by you, 5m ago")]
    [InlineData("human:anything", "Changed by you, 5m ago")]
    [InlineData("sindri-v3-engine", "Changed by sindri-v3-engine, 5m ago")]
    [InlineData("session:abc", "Changed by session:abc, 5m ago")]
    [InlineData("owner", "Changed by owner, 5m ago")]
    public async Task Only_the_persons_own_actor_reads_you_any_other_actor_shows_its_name(string by, string expected)
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["changedBy"] = by;
        _f.ServeList(list);

        var page = await _f.Shown(Token);

        Assert.Equal(expected, page.Rows[0].ChangedText);
    }

    [Theory]
    [InlineData(1, 1, "")]
    [InlineData(null, 1, "")]
    [InlineData(2, 2, "Revision 2")]
    [InlineData(null, 2, "Newest revision 2")]
    [InlineData(1, 3, "Revision 1 active, 3 newest")]
    public void The_revision_is_said_only_from_the_second_revision_on(int? active, int latest, string expected) =>
        Assert.Equal(expected, Labels.WfRevision(active, latest));

    [Fact]
    public void The_shared_refusal_line_starts_the_reason_with_a_capital_letter()
    {
        Assert.Equal("The slug is taken · SND-GRAPH-009", Labels.InstanceRefused(new Error("SND-GRAPH-009", ErrorKind.Conflict, "the slug is taken")));
        Assert.Equal("'x' has no active revision · SND-GRAPH-105", Labels.InstanceRefused(new Error("SND-GRAPH-105", ErrorKind.Conflict, "'x' has no active revision")));
    }
}
