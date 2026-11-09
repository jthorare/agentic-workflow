using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri wf list|show|runs|run-show': text and --json, read from the routes; they never start the server.</summary>
public sealed class CliWorkflowReadsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly DateTimeOffset _at = new(2026, 11, 8, 9, 30, 0, TimeSpan.Zero);

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Body<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    private WfReadVerbs Verbs(FakeControlPlaneProbe probe, Api api) =>
        new(new ControlPlaneClient(probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((Core.Ids.RepoName?)null)), handler: api),
            new Output(_out, _error, SliceHarness.Json));

    private string Text => _out.ToString().ReplaceLineEndings("\n");

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_list_prints_a_line_per_workflow_with_the_active_revision_and_who_saved_the_newest()
    {
        var rows = new ListWorkflowsResponse([
            new WorkflowSummary(_slug, "Morning check", ActiveRevision: 1, LatestRevision: 2, "session:s1", _at),
            new WorkflowSummary(WorkflowSlug.From("weekly-report"), "Weekly report", ActiveRevision: null, LatestRevision: 1, "owner", _at)]);
        var api = new Api(HttpStatusCode.OK, Body(rows));

        var code = await Verbs(_up, api).List([], Token);

        Assert.Equal((0, "/sindri/workflows"), (code, api.Path));
        Assert.Equal("morning-check  Morning check  active r1  r2 saved by session:s1 2026-11-08 09:30:00Z\nweekly-report  Weekly report  off  r1 saved by owner 2026-11-08 09:30:00Z\n", Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_list_says_so_when_there_are_no_workflows()
    {
        var code = await Verbs(_up, new Api(HttpStatusCode.OK, Body(new ListWorkflowsResponse([])))).List([], Token);

        Assert.Equal((0, "No workflows yet.\n"), (code, Text));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_list_json_prints_the_body_as_received()
    {
        var body = Body(new ListWorkflowsResponse([new WorkflowSummary(_slug, "Morning check", 1, 1, "owner", _at)]));

        var code = await Verbs(_up, new Api(HttpStatusCode.OK, body)).List(["--json"], Token);

        Assert.Equal(0, code);
        Assert.Equal(JsonNode.Parse(body)!.ToJsonString(), JsonNode.Parse(Text)!.ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_show_prints_the_revision_who_changed_it_and_the_graph()
    {
        var spec = new GraphSpec(1, "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, []), new GraphNode(NodeId.From("agent"), "run_agent", "Check the repos", [])],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("agent"))]);
        var shown = new GetWorkflowResponse(WorkflowId.From("WF-00000000000000000000000000000001"), _slug, "Morning check", Revision: 1, ActiveRevision: 1, LatestRevision: 2, ArchivedAt: null,
            "owner", "session:s1", _at, "abc123", JsonNode.Parse(GraphJson.Write(spec))!, []);
        var api = new Api(HttpStatusCode.OK, Body(shown));

        var code = await Verbs(_up, api).Show(["morning-check"], Token);

        Assert.Equal((0, "/sindri/workflows/morning-check"), (code, api.Path));
        Assert.Equal(
            "morning-check  Morning check\nshowing r1 (active r1); newest is r2, saved by session:s1 2026-11-08 09:30:00Z\nhash abc123\nnode start  manual\nnode agent  run_agent  Check the repos\nedge start -> agent\n",
            Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_show_needs_one_slug()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var code = await Verbs(_up, api).Show([], Token);

        Assert.Equal((2, 0), (code, api.Calls));
        Assert.Contains("usage: sindri wf show <slug>", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_runs_prints_a_line_per_run_and_the_empty_sentence_for_none()
    {
        var failed = new WorkflowRunSummary(WorkflowRunId.Of(_slug, 1, 2), 1, "failed", "schedule", _at, _at.AddMinutes(1), "node 'agent' failed");
        var running = new WorkflowRunSummary(WorkflowRunId.Of(_slug, 1, 3), 1, "running", "manual", _at.AddMinutes(5), EndedAt: null, Error: null);
        var api = new Api(HttpStatusCode.OK, Body(new ListWorkflowRunsResponse(_slug, [running, failed])));

        var code = await Verbs(_up, api).Runs(["morning-check"], Token);

        Assert.Equal((0, "/sindri/workflows/morning-check/runs"), (code, api.Path));
        Assert.Equal("morning-check@1:3  running  manual  2026-11-08 09:35:00Z\nmorning-check@1:2  failed  schedule  2026-11-08 09:30:00Z  node 'agent' failed\n", Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_runs_says_so_when_the_workflow_never_ran()
    {
        var code = await Verbs(_up, new Api(HttpStatusCode.OK, Body(new ListWorkflowRunsResponse(_slug, [])))).Runs(["morning-check"], Token);

        Assert.Equal((0, "No runs of morning-check yet.\n"), (code, Text));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wf_run_show_prints_the_run_and_a_line_per_node_run()
    {
        var run = new GetWorkflowRunResponse(WorkflowRunId.Of(_slug, 1, 2), _slug, 1, "failed", JsonNode.Parse("{\"kind\":\"manual\"}"), Outputs: null, "node 'agent' failed", ItemId: null, _at, _at.AddMinutes(1),
            [new NodeRunView(NodeId.From("agent"), 0, "failed", Input: null, Output: null, Truncated: false, "timed out"), new NodeRunView(NodeId.From("start"), 0, "succeeded", Input: null, Output: JsonNode.Parse("{}"), Truncated: false, Error: null)]);
        var api = new Api(HttpStatusCode.OK, Body(run));

        var code = await Verbs(_up, api).RunShow(["morning-check@1:2"], Token);

        Assert.Equal(0, code);
        Assert.Equal("/sindri/workflow-runs/morning-check@1:2", Uri.UnescapeDataString(api.Path!));
        Assert.Equal("morning-check@1:2  failed  started 2026-11-08 09:30:00Z, ended 2026-11-08 09:31:00Z\nerror: node 'agent' failed\nnode agent  failed  timed out\nnode start  succeeded\n", Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_reads_exit_1_when_serve_is_down_and_call_nothing()
    {
        var api = new Api(HttpStatusCode.OK, "{}");

        var codes = new[]
        {
            await Verbs(new FakeControlPlaneProbe(), api).List([], Token),
            await Verbs(new FakeControlPlaneProbe(), api).Show(["morning-check"], Token),
            await Verbs(new FakeControlPlaneProbe(), api).Runs(["morning-check"], Token),
            await Verbs(new FakeControlPlaneProbe(), api).RunShow(["morning-check@1:1"], Token),
        };

        Assert.Equal([1, 1, 1, 1], codes);
        Assert.Equal(0, api.Calls);
        Assert.StartsWith("SND-API-001 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_workflow_prints_the_servers_refusal_and_exits_2()
    {
        var api = new Api(HttpStatusCode.NotFound, "{\"type\":\"about:blank\",\"title\":\"SND-GRAPH-008\",\"status\":404,\"detail\":\"no workflow 'nope'; list the workflows to see what exists\"}");

        var code = await Verbs(_up, api).Show(["nope"], Token);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-GRAPH-008 ", _error.ToString(), StringComparison.Ordinal);
    }

    private sealed class Api(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Path = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, (int)status >= 400 ? "application/problem+json" : "application/json") });
        }
    }
}
