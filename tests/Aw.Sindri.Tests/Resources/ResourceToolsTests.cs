using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Features.ListResources;
using Aw.Sindri.Features.PurgeResources;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.Resources;

/// <summary>The resources_list and resources_purge MCP tools, called in process over a resource store on a temp dir and the in-memory workflow ports.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ResourceToolsTests : IDisposable
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("triage");

    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;

    public ResourceToolsTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
    }

    public void Dispose() => _home.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public async Task resources_list_returns_the_owners_entries()
    {
        Provision("workflow:triage");
        Provision("toolkit:mail");
        await using var services = Services();

        var result = await ResourceTools.Call(ResourceTools.List, services, Arguments(("owner", "workflow:triage")), Token);

        Assert.True(result.IsError != true);
        var text = Text(result);
        Assert.Contains("workflow:triage", text, StringComparison.Ordinal);
        Assert.Contains("state", text, StringComparison.Ordinal);
        Assert.Contains("scratch", text, StringComparison.Ordinal);
        Assert.DoesNotContain("toolkit:mail", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task resources_purge_with_no_queued_or_running_run_deletes_the_owners_folder_and_returns_the_removed_count()
    {
        Provision("workflow:triage");
        Provision("workflow:other");
        await using var services = Services();

        var result = await ResourceTools.Call(ResourceTools.Purge, services, Arguments(("owner", "workflow:triage")), Token);

        Assert.True(result.IsError != true);
        Assert.Contains("purged 2 resources of workflow:triage", Text(result), StringComparison.Ordinal);   // the database and the disk
        Assert.False(Directory.Exists(Path.Combine(Root, "workflow", "triage")));
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "other", "state.db")));   // nobody else's folder was touched
    }

    [Fact]
    public async Task resources_purge_with_a_queued_run_is_a_tool_error_with_SND_RES_003_and_keeps_the_files()
    {
        await CreateWorkflow();
        Provision("workflow:triage");
        var queued = new WorkflowRun(WorkflowRunId.Of(_slug, 1, 1), _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Queued, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
        _ = (await _runs.Insert(queued, Token)).Value;
        await using var services = Services();

        var result = await ResourceTools.Call(ResourceTools.Purge, services, Arguments(("owner", "workflow:triage")), Token);

        Assert.True(result.IsError);
        Assert.Contains("SND-RES-003", Text(result), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "triage", "state.db")));
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "triage", "scratch", "note.txt")));
    }

    [Fact]
    public async Task resources_purge_with_a_malformed_owner_is_a_tool_error_with_SND_RES_006()
    {
        Provision("workflow:triage");
        await using var services = Services();

        var result = await ResourceTools.Call(ResourceTools.Purge, services, Arguments(("owner", "nope")), Token);

        Assert.True(result.IsError);
        Assert.Contains("SND-RES-006", Text(result), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Root, "workflow", "triage", "state.db")));
    }

    private static Dictionary<string, JsonElement> Arguments(params (string Key, object Value)[] pairs)
        => pairs.ToDictionary(static p => p.Key, static p => JsonSerializer.SerializeToElement(p.Value), StringComparer.Ordinal);

    private static string Text(CallToolResult result)
        => (result.StructuredContent?.GetRawText() ?? string.Empty) + string.Concat(result.Content.OfType<TextContentBlock>().Select(static t => t.Text));

    private ServiceProvider Services()
    {
        var files = new ResourceFiles(Root);
        var slice = new SliceHarness();
        return new ServiceCollection()
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddSingleton<IService<ListResourcesRequest, ListResourcesResponse>>(new ListResourcesService(files))
            .AddSingleton<IService<PurgeResourcesRequest, PurgeResourcesResponse>>(new PurgeResourcesService(files, _workflows, _runs, new ResourceEvents(slice.Held, slice.Store, slice.Events, _clock)))
            .BuildServiceProvider();
    }

    private async Task CreateWorkflow() =>
        _ = (await _workflows.Create(_id, _slug, new GraphSpec(Version: 1, "Triage", Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []), "owner", Token)).Value;

    private void Provision(string owner)
    {
        var parsed = ResourceOwner.Parse(owner).Value;
        var plan = ResourcePlanner.Plan(parsed, new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []), Root).Value;
        Assert.True(new ResourceStore(Root).Provision(parsed, plan).IsSuccess);
        File.WriteAllText(Path.Combine(Root, parsed.Folder, "scratch", "note.txt"), "kept");
    }
}
