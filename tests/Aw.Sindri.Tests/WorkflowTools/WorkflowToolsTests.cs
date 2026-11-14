using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.RerunWorkflowRun;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Features.SearchWorkflowDocs;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.WorkflowTools;

/// <summary>The workflow_* MCP tools, called in process over fakes. (tools/list is a golden the lead regenerates.)</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowToolsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, JsonElement> Arguments(params (string Name, object Value)[] pairs) =>
        pairs.ToDictionary(static p => p.Name, static p => JsonSerializer.SerializeToElement(p.Value), StringComparer.Ordinal);

    private static JsonNode Structured(CallToolResult result) => JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!;   // why: every tool result carries structuredContent

    [Fact]
    public void tools_list_includes_every_workflow_tool_with_an_object_schema()
    {
        var names = new[] { "workflow_list", "workflow_get", "workflow_runs", "workflow_run_get", "workflow_create", "workflow_apply_ops", "workflow_replace", "workflow_validate", "workflow_activate", "workflow_pause", "workflow_delete", "workflow_run", "workflow_rerun", "workflow_cancel", "workflow_docs", "workflow_blocks" };

        Tool[] tools = [.. WorkflowReadTools.All, .. Host.Mcp.WorkflowTools.All];

        Assert.Equal(names, tools.Select(static t => t.Name), StringComparer.Ordinal);
        Assert.All(tools, static t => Assert.Equal("object", t.InputSchema.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task workflow_blocks_groups_blocks_by_group_and_shows_each_title()
    {
        var catalog = new Catalog([new Block("schedule", "Schedule", BlockGroup.Triggers), new Block("memory_search", "Search memory", BlockGroup.Data), new Block("prompt", "Ask the model", BlockGroup.AI), new Block("condition", "Condition", BlockGroup.Logic), new Block("notify", "Notify me", BlockGroup.Outputs), new Block("manual", "Manual", BlockGroup.Triggers)]);
        await using var services = Services(s => s.AddSingleton<IService<ListWorkflowBlocksRequest, ListWorkflowBlocksResponse>>(new ListWorkflowBlocksService(catalog, new Docs([]))));

        var result = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Blocks, services, arguments: null, Token);

        Assert.True(result.IsError != true);
        var groups = Structured(result)["groups"]!.AsArray();
        Assert.Equal(["Triggers", "Data", "AI", "Logic", "Outputs"], groups.Select(static g => g!["group"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(["Manual", "Schedule"], groups[0]!["blocks"]!.AsArray().Select(static b => b!["title"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("Ask the model", groups[2]!["blocks"]![0]!["title"]!.GetValue<string>());
        Assert.Equal("memory_search", groups[1]!["blocks"]![0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task workflow_docs_reads_a_page_ranks_a_question_and_refuses_an_unknown_page()
    {
        var docs = new Docs([new WorkflowDocPage("spec", "The spec", "# The spec\nnodes and edges"), new WorkflowDocPage("block-schedule", "Schedule", "# Schedule\nRun on a cron timer")]);
        await using var services = Services(s => s.AddSingleton<IService<SearchWorkflowDocsRequest, SearchWorkflowDocsResponse>>(new SearchWorkflowDocsService(docs)));

        var page = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Docs, services, Arguments(("page", "spec")), Token);
        var ranked = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Docs, services, Arguments(("question", "cron timer")), Token);
        var unknown = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Docs, services, Arguments(("page", "nope")), Token);

        Assert.Equal("nodes and edges", Structured(page)["page"]!["text"]!.GetValue<string>().Split('\n')[1]);
        Assert.Equal("block-schedule", Structured(ranked)["hits"]![0]!["slug"]!.GetValue<string>());
        Assert.True(unknown.IsError);
        Assert.Equal("SND-GRAPH-008", Structured(unknown)["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task workflow_run_refuses_a_session_a_run_of_the_same_workflow_launched()
    {
        var slug = WorkflowSlug.From("drift-check");
        var run = WorkflowRunId.Of(slug, 3, 12);
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        var ids = new SequentialIdGenerator();
        var callers = new AsyncLocalCallerContextAccessor(ids);
        var clock = TimeProvider.System;
        var service = new RunWorkflowService(new InMemoryWorkflowStore(clock), new InMemoryWorkflowRunStore(clock), new NeverStarter(), new Launches(run), callers, clock);   // why: the guard lives in the service now (#2452); the tool must still answer its refusal
        await using var services = Services(s => s.AddSingleton<ICallerContextAccessor>(callers).AddSingleton<IService<RunWorkflowRequest, RunWorkflowResponse>>(service));
        using var caller = callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:s1", ids.New<CommandId>(), ids.New<CorrelationId>(), Repo: null, launch));

        var result = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Run, services, Arguments(("slug", "drift-check")), Token);

        Assert.True(result.IsError);
        Assert.Equal("SND-GRAPH-109", Structured(result)["code"]!.GetValue<string>());
        Assert.Contains(run.Value, Structured(result)["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task workflow_rerun_passes_run_from_and_input_to_the_service_and_answers_the_new_run()
    {
        var service = new RecordingRerun();
        var callers = new AsyncLocalCallerContextAccessor(new SequentialIdGenerator());
        await using var services = Services(s => s.AddSingleton<ICallerContextAccessor>(callers).AddSingleton<IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse>>(service));

        var result = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Rerun, services, Arguments(("run", "drift-check@3:12"), ("from", "send"), ("input", new { repo = "web" })), Token);

        Assert.True(result.IsError != true);
        var request = Assert.Single(service.Requests);
        Assert.Equal(WorkflowRunId.Of(WorkflowSlug.From("drift-check"), 3, 12), request.Run);
        Assert.Equal("send", request.FromNode);
        Assert.Equal("web", JsonNode.Parse(request.Input!)!["repo"]!.GetValue<string>());
        Assert.Equal("drift-check@3:13", Structured(result)["run"]!.GetValue<string>());
    }

    [Fact]
    public async Task workflow_rerun_without_from_or_with_an_array_input_is_refused_before_the_service()
    {
        var service = new RecordingRerun();
        await using var services = Services(s => s.AddSingleton<IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse>>(service));

        var noFrom = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Rerun, services, Arguments(("run", "drift-check@3:12")), Token);
        var badInput = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Rerun, services, Arguments(("run", "drift-check@3:12"), ("from", "send"), ("input", new[] { 1 })), Token);

        Assert.True(noFrom.IsError);
        Assert.True(badInput.IsError);
        Assert.Empty(service.Requests);
    }

    private sealed class RecordingRerun : IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse>
    {
        public List<RerunWorkflowRunRequest> Requests { get; } = [];

        public ValueTask<Result<RerunWorkflowRunResponse>> Handle(RerunWorkflowRunRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var next = WorkflowRunId.Of(WorkflowSlug.From("drift-check"), 3, 13);
            return ValueTask.FromResult<Result<RerunWorkflowRunResponse>>(new RerunWorkflowRunResponse(next, "started", request.Run, ["a"], ["send"]));
        }
    }

    private static ServiceProvider Services(Action<IServiceCollection> add)
    {
        var collection = new ServiceCollection().AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default));
        add(collection);
        return collection.BuildServiceProvider();
    }

    private sealed class Catalog(IEnumerable<IGraphBlockType> blocks) : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = blocks.ToDictionary(static b => b.Type, StringComparer.Ordinal);
    }

    private sealed class Block(string type, string title, BlockGroup group) : IGraphBlockType
    {
        public string Type => type;

        public string Title => title;

        public BlockGroup Group => group;

        public bool IsTrigger => group == BlockGroup.Triggers;

        public BlockSchema Config { get; } = new([new BlockField("name", BlockFieldType.Text)]);

        public string Example => "{\"name\":\"x\"}";

        public BlockOutputs OutputsFor(JsonObject config) => new(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed class Docs(IReadOnlyList<WorkflowDocPage> pages) : IWorkflowDocs
    {
        public IReadOnlyList<WorkflowDocPage> Pages => pages;
    }

    // The loop guard refuses before any start, so a start here is a test failure.
    private sealed class NeverStarter : IGraphRunStarter
    {
        public ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("the guard should have refused before a start");
    }

    private sealed class Launches(WorkflowRunId run) : IGraphLaunchRuns
    {
        public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<WorkflowRunId?>>(run);
    }
}
