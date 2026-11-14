using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
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
    public async Task A_name_no_other_workflow_has_shows_no_short_id_beside_it()
    {
        var page = await _f.Shown(Token);

        Assert.All(page.Rows, row => Assert.False(row.ShowsId));   // "Morning check" and "Weekly report": the short id waits for a duplicate name
    }

    [Fact]
    public async Task Two_workflows_with_one_name_show_the_short_id_beside_it()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![1]!["name"] = "Morning check";
        _f.ServeList(list);

        var page = await _f.Shown(Token);

        Assert.All(page.Rows, row => Assert.True(row.ShowsId));
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
    [InlineData(null, 2, "")]
    [InlineData(1, 3, "Revision 1 active, 3 newest")]
    public void The_revision_is_said_only_from_the_second_revision_on(int? active, int latest, string expected) =>
        Assert.Equal(expected, Labels.WfRevision(active, latest));

    [Fact]
    public async Task A_row_of_a_workflow_with_no_active_revision_says_no_revision()
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["activeRevision"] = null;
        list["workflows"]![0]!["latestRevision"] = 2;
        _f.ServeList(list);

        var page = await _f.Shown(Token);

        Assert.Equal((string.Empty, false), (page.Rows[0].RevisionText, page.Rows[0].HasRevisionText));
    }

    [Fact]
    public async Task The_page_of_a_workflow_with_no_active_revision_says_no_revision_and_one_with_an_active_revision_keeps_its_words()
    {
        var off = Golden("workflows-slug", "found");
        off["activeRevision"] = null;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, off.ToJsonString());
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal("2 nodes · created by owner", page.Runs.DetailText);
    }

    [Fact]
    public async Task The_page_says_created_by_you_for_the_persons_own_actor()
    {
        var made = Golden("workflows-slug", "found");
        made["createdBy"] = "human:ui";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, made.ToJsonString());
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal("Revision 1 of 2 · 2 nodes · created by you", page.Runs.DetailText);
    }

    [Fact]
    public async Task The_runs_line_names_no_revision_while_the_workflow_has_only_its_first()
    {
        var first = Golden("workflows-slug", "found");
        first["revision"] = 1;
        first["latestRevision"] = 1;
        first["activeRevision"] = 1;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, first.ToJsonString());
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal("2 nodes · created by owner", page.Runs.DetailText);   // #2845: like the list row, a first revision is not news
    }

    [Fact]
    public async Task The_runs_line_names_the_revision_once_there_is_a_second()
    {
        var second = Golden("workflows-slug", "found");
        second["revision"] = 2;
        second["latestRevision"] = 2;
        second["activeRevision"] = 2;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, second.ToJsonString());
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal("Revision 2 · 2 nodes · created by owner", page.Runs.DetailText);
    }

    [Fact]
    public void The_shared_refusal_line_starts_the_reason_with_a_capital_letter()
    {
        Assert.Equal("The slug is taken · SND-GRAPH-009", Labels.InstanceRefused(new Error("SND-GRAPH-009", ErrorKind.Conflict, "the slug is taken")));
        Assert.Equal("'x' has no active revision · SND-GRAPH-105", Labels.InstanceRefused(new Error("SND-GRAPH-105", ErrorKind.Conflict, "'x' has no active revision")));
    }
}
