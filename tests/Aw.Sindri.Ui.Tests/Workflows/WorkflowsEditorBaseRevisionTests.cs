using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The editor reads and writes the same revision (#3815): on a workflow whose live revision (1) is older than its newest (2), the step settings sheet shows revision 2's values, sends its ops on base 2, and a stale refusal's Reload reads the newest revision again.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsEditorBaseRevisionTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The found golden: revision 1 live, revision 2 newest. The plain read serves revision 1 ("Look for drift"); ?revision=latest serves revision 2 with the prompt only it holds.
    private void Serve(int latest, string newestPrompt)
    {
        var live = Golden("workflows-slug", "found");
        live["latestRevision"] = latest;
        var newest = Golden("workflows-slug", "found");
        (newest["revision"], newest["latestRevision"]) = (latest, latest);
        newest["spec"]!["nodes"]![1]!["config"]!["prompt"] = newestPrompt;   // why: the golden has two nodes, the second with a config
        newest["summaries"]![1]!["summary"] = "Run agent on the newest revision";   // why: the golden has a summary per node; the graph's step line shows this one
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, live.ToJsonString());
        _f.Server.WorkflowNewestAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newest.ToJsonString());
    }

    private async Task<WorkflowsPageViewModel> SheetOnAgent()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        Serve(latest: 2, newestPrompt: "Look for drift in revision 2");
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("agent");   // why: OpenWorkflow read the golden, so the graph is drawn
        await page.OpenSettings(Token);
        return page;
    }

    [Fact]
    public async Task The_sheet_on_a_workflow_with_a_newer_saved_revision_shows_the_newest_revisions_values()
    {
        var page = await SheetOnAgent();

        Assert.Equal(("Look for drift in revision 2", 2), (page.Editor.Fields[0].Text, page.Editor.BaseRevision));   // not revision 1's "Look for drift"
    }

    [Fact]
    public async Task The_graph_in_edit_mode_draws_the_revision_the_sheet_edits()
    {
        var page = await SheetOnAgent();

        Assert.Contains("Run agent on the newest revision", page.Runs.Graph!.StepLine, StringComparison.Ordinal);   // why: the sheet opened on a drawn graph
        Assert.Equal("Saved as revision 2. Not live yet.", page.SavedLineText);   // the page says the edits land on a revision that is not live
    }

    [Fact]
    public async Task Saving_the_sheet_sends_its_ops_on_the_revision_it_shows()
    {
        var page = await SheetOnAgent();
        page.Editor.Fields[0].Text = "Look twice";

        await page.Save(Token);

        var sent = _f.Server.AppliedWorkflowOps.Single();
        Assert.Equal(2, sent.BaseRevision);
        Assert.Contains("Look twice", sent.Ops, StringComparison.Ordinal);
    }

    [Fact]
    public async Task After_a_stale_refusal_Reload_reads_the_newest_revision_and_bases_on_it()
    {
        var page = await SheetOnAgent();
        page.Editor.Fields[0].Text = "Look twice";
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        await page.Save(Token);
        Assert.True(page.Editor.NeedsReload);

        Serve(latest: 3, newestPrompt: "Saved elsewhere as revision 3");
        await page.ReloadEditor(Token);

        Assert.Equal((false, 3, "Saved elsewhere as revision 3"), (page.Editor.NeedsReload, page.Editor.BaseRevision, page.Editor.Fields[0].Text));
    }
}
