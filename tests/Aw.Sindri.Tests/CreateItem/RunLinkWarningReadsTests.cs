using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>
/// A 'create_item' node whose item was made but whose run link was not recorded ends Succeeded with a 'warning' in its output (#2606). The warning has to reach the two readers an owner or an agent uses:
/// 'sindri wf run-show --json' and the MCP tool 'workflow_run_get'. The tests check the field and the run id it names, not the wording.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RunLinkWarningReadsTests : IAsyncLifetime
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("make-items");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 1, 1);
    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));

    private GoldenHost? _host;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GoldenHost Host => _host ?? throw new InvalidOperationException("the host isn't started");

    public async ValueTask InitializeAsync()
    {
        _host = await GoldenHost.Start(temporary: true, Token, SeededMcp.Profile);
        var spec = new GraphSpec(Version: 1, "Make items", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, []), new GraphNode(NodeId.From("make"), "create_item", Title: null, new JsonObject { ["title"] = "Loose end" })],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("make"))]);
        var workflows = Host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        var runs = Host.App.Services.GetRequiredService<IWorkflowRunStore>();
        _ = (await runs.Insert(new WorkflowRun(_run, _id, 1, """{"kind":"manual"}""", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, Host.Now(), EndedAt: Host.Now()), Token)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(_run, NodeId.From("make"), 0, NodeRunStatus.Succeeded, Input: null, Output: await WarnedOutput(), Truncated: false, Error: null, Usage: null), Token)).Value;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    // The node's output as the real block writes it: the real maker makes the item on a ledger that refuses the run link, and the block turns its warning into the 'warning' field.
    private static async Task<string> WarnedOutput()
    {
        var slice = new SliceHarness();
        _ = await slice.HoldLease();
        var runs = new InMemoryWorkflowRunStore(new FakeTimeProvider(KnownInstants.Epoch));
        _ = (await runs.Insert(new WorkflowRun(_run, _id, 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null), Token)).Value;
        var maker = new WorkflowItemMaker(new CreateItemService(slice.Profiles, slice.Held, new LinkRefusingLedger(slice.Store), slice.Events, slice.Ids, slice.Callers), slice.Callers, runs);
        var config = new JsonObject { ["title"] = "Loose end" };

        var result = await new CreateItemBlock(maker).Execute(new BlockContext(_run, NodeId.From("make"), config, (JsonObject)config.DeepClone(), _ => null), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return new JsonObject(result.Value.Outputs.Select(static o => KeyValuePair.Create(o.Key, o.Value?.DeepClone()))).ToJsonString();
    }

    // The warning field of a node's output; the readers may fence the output as one string, so the JSON is cut out of it.
    private static string WarningOf(JsonElement output)
    {
        var text = output.ValueKind == JsonValueKind.String ? output.GetString()! : output.GetRawText();
        var json = JsonNode.Parse(text[text.IndexOf('{', StringComparison.Ordinal)..(text.LastIndexOf('}') + 1)])!.AsObject();
        Assert.True(json.ContainsKey("itemId"));
        return json["warning"]?.GetValue<string>() ?? string.Empty;
    }

    private static JsonElement NodeOutput(JsonElement run) =>
        run.GetProperty("nodes").EnumerateArray().Single(static n => n.GetProperty("node").GetString() == "make").GetProperty("output");

    [Fact(Timeout = 60_000)]
    public async Task Wf_run_show_json_carries_the_create_item_warning_and_names_the_run()
    {
        var ct = TestContext.Current.CancellationToken;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var client = new ControlPlaneClient(_up, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult<RepoName?>(RepoName.From("alpha"))), handler: new ForwardToHost(Host.Http));

        var code = await new WfReadVerbs(client, new Output(stdout, stderr, SliceHarness.Json)).RunShow([_run.Value, "--json"], ct);

        Assert.True(code == 0, stderr.ToString());
        var warning = WarningOf(NodeOutput(JsonDocument.Parse(stdout.ToString()).RootElement));
        Assert.False(string.IsNullOrWhiteSpace(warning));
        Assert.Contains(_run.Value, warning, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000)]
    public async Task The_workflow_run_get_tool_carries_the_create_item_warning_and_names_the_run()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mcp = await McpSession.Open(Host, "alpha", ct);

        var result = await mcp.Call("workflow_run_get", $"{{\"run\":\"{_run.Value}\"}}", ct);

        Assert.NotEqual(true, result.IsError);
        var warning = WarningOf(NodeOutput(result.StructuredContent!.Value));
        Assert.False(string.IsNullOrWhiteSpace(warning));
        Assert.Contains(_run.Value, warning, StringComparison.Ordinal);
    }

    // Sends the CLI's request, with its caller headers, to the running host.
    private sealed class ForwardToHost(HttpClient host) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri!.PathAndQuery);
            foreach (var header in request.Headers)
            {
                _ = forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return await host.SendAsync(forwarded, cancellationToken);
        }
    }
}
