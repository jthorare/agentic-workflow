using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The server's validation findings in the Workflows editor (#3803): a mark on the step they belong to, a count in the header, the findings of the selected step, and the fix for GRAPH-PROMPT-001.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowFindingsTests
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

    private const string Many = """
        {"passed":false,"hash":"sha256:0000000000000000000000000000000000000000000000000000000000000003","findings":[
          {"code":"GRAPH-PROMPT-001","severity":"warning","nodes":["agent"],"field":"config.prompt","message":"prompt node 'agent' reads no data","fix":"reference a step's output in the question","id":"GRAPH-PROMPT-001:agent:config.prompt","node":"agent","cause":"prompt node 'agent' reads no data"},
          {"code":"GRAPH-CONFIG-001","severity":"error","nodes":["agent"],"field":"config.prompt","message":"node 'agent' is missing its prompt","fix":"set config.prompt","id":"GRAPH-CONFIG-001:agent:config.prompt","node":"agent","cause":"node 'agent' is missing its prompt"},
          {"code":"GRAPH-NAME-001","severity":"warning","nodes":[],"field":"name","message":"the workflow has no name","fix":"give it a name","id":"GRAPH-NAME-001::name","node":null,"cause":"the workflow has no name"}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnRuns(RouteAnswer findings)
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        _f.Server.ValidateWorkflowAnswer = findings;
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.FindingsRead;
        return page;
    }

    private static GraphNodeViewModel Node(WorkflowsPageViewModel page, string id) => page.Runs.Graph!.Nodes.Single(n => string.Equals(n.Id, id, StringComparison.Ordinal));

    [Fact]
    public async Task Opening_a_workflow_asks_the_server_to_validate_it()
    {
        _ = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "passed"));

        Assert.Equal([Slug], _f.Server.ValidatedWorkflows, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Validate_checks_the_revision_the_graph_draws_not_the_newest()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "passed"));

        Assert.Equal((1, 2), (page.Runs.Shown!.Revision, page.Runs.Shown!.LatestRevision));   // the golden runs revision 1 and holds a newer 2: the graph draws 1
        Assert.Equal(1, Assert.Single(_f.Server.ValidatedRevisions));
    }

    [Fact]
    public async Task No_findings_mark_no_step_and_leave_the_header_empty()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "passed"));
        _ = page.Runs.Graph!.SelectNode("agent");

        Assert.Equal((false, string.Empty), (page.Findings.HasFindings, page.Findings.HeaderText));
        Assert.All(page.Runs.Graph.Nodes, n => Assert.Equal((0, string.Empty), (n.FindingCount, n.FindingMark)));
        Assert.Empty(page.StepFindings);
    }

    [Fact]
    public async Task A_prompt_that_reads_no_data_marks_its_step_and_shows_the_servers_words()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data"));

        var agent = Node(page, "agent");
        Assert.Equal((1, false, "⚠ 1"), (agent.FindingCount, agent.IsFindingError, agent.FindingMark));
        Assert.Equal(0, Node(page, "start").FindingCount);
        Assert.Equal("1 thing to fix", page.Findings.HeaderText);
        Assert.Contains("1 thing to fix", agent.Name, StringComparison.Ordinal);

        _ = page.Runs.Graph!.SelectNode("agent");

        var finding = Assert.Single(page.StepFindings);
        Assert.StartsWith("prompt node 'agent' reads no data: its question has no {{...}} reference", finding.Sentence, StringComparison.Ordinal);
        Assert.StartsWith("add a step before 'agent' that fetches the data", finding.FixLine, StringComparison.Ordinal);
        Assert.Equal((true, false, "attention"), (finding.HasFix, finding.IsError, finding.ToneWord));
    }

    [Fact]
    public async Task Many_findings_count_per_step_and_an_error_wins_the_mark()
    {
        var page = await OnRuns(new RouteAnswer(HttpStatusCode.OK, Many));

        var agent = Node(page, "agent");
        Assert.Equal((2, true, "✕ 2"), (agent.FindingCount, agent.IsFindingError, agent.FindingMark));
        Assert.Equal("3 things to fix", page.Findings.HeaderText);
        Assert.Empty(page.StepFindings);   // no step selected: the sheet's list is empty

        _ = page.Runs.Graph!.SelectNode("start");
        Assert.Empty(page.StepFindings);

        _ = page.Runs.Graph.SelectNode("agent");
        Assert.Equal(["GRAPH-PROMPT-001", "GRAPH-CONFIG-001"], page.StepFindings.Select(f => f.Code), StringComparer.Ordinal);
        Assert.Equal((false, true, "error"), (page.StepFindings[1].HasFix, page.StepFindings[1].IsError, page.StepFindings[1].ToneWord));   // only GRAPH-PROMPT-001 has a fix button
    }

    [Fact]
    public async Task Fix_opens_the_steps_sheet_on_its_question_and_asks_for_the_field_picker()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data"));
        var finding = Assert.Single(page.Findings.All);

        await page.FixFinding(finding, Token);

        Assert.Equal((true, "agent"), (page.IsSettingsOpen, page.Runs.Graph!.SelectedId));
        Assert.Equal(("prompt", true), (page.FocusFieldName, page.IsFieldPickerAsked));
        Assert.Contains(page.Editor.Fields, f => string.Equals(f.Name, "prompt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Closing_the_sheet_forgets_the_field_to_focus()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data"));
        await page.FixFinding(Assert.Single(page.Findings.All), Token);

        page.CloseSettings();

        var sheetDefault = (page.Editor.Fields.FirstOrDefault(static f => f.IsRequired) ?? page.Editor.Fields.FirstOrDefault())?.Name ?? string.Empty;
        Assert.Equal((sheetDefault, false), (page.FocusFieldName, page.IsFieldPickerAsked));
    }
}
