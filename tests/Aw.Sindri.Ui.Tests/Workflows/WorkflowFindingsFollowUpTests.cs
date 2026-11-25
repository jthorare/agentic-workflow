using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// The follow-ups of the findings panel (#4380, from the review of #4378): a finding about the whole workflow is listed, not only counted; a failed check says so and asks again on
/// the next open; Fix is hidden when the editor is read-only; an answer that arrives late for an older ask never shows.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowFindingsFollowUpTests
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

    private const string WholeAndStep = """
        {"passed":false,"hash":"sha256:0000000000000000000000000000000000000000000000000000000000000003","findings":[
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

    private void ServeRevision(string slug, int revision)
    {
        var golden = Golden("workflows-slug", "found");
        (golden["slug"], golden["revision"], golden["latestRevision"], golden["specHash"]) = (slug, revision, revision, "sha256:" + new string('f', 64));
        _f.Server.WorkflowAnswers[slug] = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task A_finding_about_the_whole_workflow_is_listed_and_counted()
    {
        var page = await OnRuns(new RouteAnswer(HttpStatusCode.OK, WholeAndStep));

        var whole = Assert.Single(page.Findings.WholeWorkflow);
        Assert.Equal(("GRAPH-NAME-001", true), (whole.Code, page.Findings.HasWholeWorkflow));
        Assert.Equal("2 things to fix", page.Findings.HeaderText);   // the count and the lists add up: one on the step, one here
        Assert.Equal(["GRAPH-CONFIG-001"], page.Findings.For("agent").Select(static f => f.Code), StringComparer.Ordinal);
    }

    [Fact]
    public async Task No_whole_workflow_finding_lists_nothing()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data"));

        Assert.Equal((false, 0), (page.Findings.HasWholeWorkflow, page.Findings.WholeWorkflow.Count));
    }

    [Fact]
    public async Task A_failed_check_says_so_and_the_next_open_asks_again()
    {
        var page = await OnRuns(RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-GRAPH-008", "no such workflow"));

        Assert.Equal((true, false), (page.Findings.HasFailed, page.Findings.HasFindings));
        Assert.False(string.IsNullOrWhiteSpace(page.Findings.FailedText));

        _f.Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data");
        await page.OpenWorkflow(Slug, Token);
        await page.FindingsRead;

        Assert.Equal(2, _f.Server.ValidatedWorkflows.Count);
        Assert.Equal((false, true), (page.Findings.HasFailed, page.Findings.HasFindings));
    }

    [Fact]
    public async Task Fix_is_hidden_and_does_nothing_on_an_archived_workflow()
    {
        var golden = Golden("workflows-slug", "found");
        golden["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());

        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data"));

        var finding = Assert.Single(page.Findings.All);
        Assert.Equal((false, false), (page.CanEdit, finding.HasFix));
        await page.FixFinding(finding, Token);
        Assert.False(page.IsSettingsOpen);
    }

    [Fact]
    public async Task Fix_shows_on_an_editable_workflow()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data"));

        Assert.Equal((true, true), (page.CanEdit, Assert.Single(page.Findings.All).HasFix));
    }

    [Fact]
    public async Task An_answer_for_an_older_revision_that_arrives_late_never_shows()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        _f.Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data");
        var gate = _f.Server.ValidateWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        var first = page.FindingsRead;   // asked, held

        ServeRevision(Slug, 3);   // the workflow moved on: a newer ask, answered at once with nothing found
        _f.Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "passed");
        await page.OpenWorkflow(Slug, Token);
        await page.FindingsRead;
        gate.SetResult();
        await first;

        Assert.Equal((false, 0), (page.Findings.HasFindings, page.Runs.Graph!.Nodes.Sum(static n => n.FindingCount)));
    }

    [Fact]
    public async Task An_answer_for_another_workflow_that_arrives_late_never_shows()
    {
        const string other = "evening-check";
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        _f.Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data");
        var gate = _f.Server.ValidateWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        var first = page.FindingsRead;

        ServeRevision(other, 1);
        _f.Server.WorkflowRunsAnswers[other] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        _f.Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "passed");
        await page.OpenWorkflow(other, Token);
        await page.FindingsRead;
        gate.SetResult();
        await first;

        Assert.Equal((other, false), (page.Runs.Slug, page.Findings.HasFindings));
    }

    [Fact]
    public async Task A_failed_check_can_be_retried_and_the_line_no_longer_waits_for_the_next_open()
    {
        var page = await OnRuns(RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-GRAPH-008", "no such workflow"));
        Assert.True(page.Findings.HasFailed);
        Assert.DoesNotContain("open", page.Findings.FailedText, StringComparison.OrdinalIgnoreCase);   // the person is on the workflow already: "when you open it" is not something to act on

        _f.Server.ValidateWorkflowAnswer = RouteAnswer.SpecShaped("workflows-validate", "prompt-reads-no-data");
        await page.RetryFindings();

        Assert.Equal(2, _f.Server.ValidatedWorkflows.Count);
        Assert.Equal((false, true), (page.Findings.HasFailed, page.Findings.HasFindings));
        Assert.Equal(page.Runs.Shown!.Revision, _f.Server.ValidatedRevisions[1]);   // the shown revision, not the newest
    }

    [Fact]
    public async Task Fix_shows_once_the_page_is_back_on_the_workflow_after_findings_came_in_on_a_run_page()
    {
        var page = await OnRuns(RouteAnswer.SpecShaped("workflows-validate", "passed"));
        await page.OpenRun(RunId, Token);   // the run page: no editor, so a fix cannot be offered while the findings arrive
        var asked = new WorkflowValidated(
            Passed: false, Hash: null,
            [new("GRAPH-PROMPT-001", "warning", ["agent"], "config.prompt", "reads no data", "add a step", "GRAPH-PROMPT-001:agent:config.prompt", "agent", "reads no data")]);
        page.Findings.Show(asked);
        Assert.False(Assert.Single(page.Findings.All).HasFix);

        page.Back();

        Assert.True(Assert.Single(page.Findings.All).HasFix);   // the editor is there again: Fix comes back with it
    }
}
