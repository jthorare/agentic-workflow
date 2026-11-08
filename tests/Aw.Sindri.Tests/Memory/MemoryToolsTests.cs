using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The memory_* MCP tools, called in process over the in-memory ports. (tools/list is a golden the lead regenerates: five tools were added to the table.)</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryToolsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ServiceProvider Services(MemoryHarness h) => new ServiceCollection()
        .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
        .AddSingleton<IService<CreateMemoryRequest, CreateMemoryResponse>>(h.Create())
        .AddSingleton<IService<ListMemoriesRequest, ListMemoriesResponse>>(h.List())
        .AddSingleton<IService<WriteMemoryFactRequest, WriteMemoryFactResponse>>(h.Write())
        .AddSingleton<IService<SearchMemoryRequest, SearchMemoryResponse>>(h.Search())
        .AddSingleton<IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse>>(h.Forget())
        .BuildServiceProvider();

    private static Dictionary<string, JsonElement> Arguments(params (string Key, object Value)[] pairs)
        => pairs.ToDictionary(static p => p.Key, static p => JsonSerializer.SerializeToElement(p.Value), StringComparer.Ordinal);

    private static string Text(CallToolResult result)
        => (result.StructuredContent?.GetRawText() ?? string.Empty) + string.Concat(result.Content.OfType<TextContentBlock>().Select(static t => t.Text));

    [Fact]
    public async Task An_owner_session_reads_and_writes_the_same_store_over_MCP()
    {
        var h = new MemoryHarness();
        _ = h.Embedder.With("Deploys run from main", 1f, 0f, 0f, 0f).With("how do we deploy", 1f, 0f, 0f, 0f);
        await using var services = Services(h);

        var created = await MemoryTools.CallCreate(services, Arguments(("name", "team-notes")), Token);
        var written = await MemoryTools.CallWrite(services, Arguments(("name", "team-notes"), ("text", "Deploys run from main"), ("tags", new[] { "ops" })), Token);
        var recalled = await MemoryTools.CallSearch(services, Arguments(("name", "team-notes"), ("query", "how do we deploy")), Token);
        var listed = await MemoryTools.CallList(services, new Dictionary<string, JsonElement>(StringComparer.Ordinal), Token);

        Assert.True(created.IsError != true);
        Assert.Contains("added", Text(written), StringComparison.Ordinal);
        Assert.Contains("Deploys run from main", Text(recalled), StringComparison.Ordinal);
        Assert.Contains("semantic", Text(recalled), StringComparison.Ordinal);
        Assert.Contains("team-notes", Text(listed), StringComparison.Ordinal);
        Assert.Single(h.Store.AllFacts);   // the tools and a CLI or HTTP caller share one store
    }

    [Fact]
    public async Task memory_search_of_an_unknown_memory_is_a_tool_error_with_the_code()
    {
        await using var services = Services(new MemoryHarness());

        var result = await MemoryTools.CallSearch(services, Arguments(("name", "nobody"), ("query", "anything")), Token);

        Assert.True(result.IsError);
        Assert.Contains("SND-MEM-001", Text(result), StringComparison.Ordinal);
    }

    [Fact]
    public void The_five_tools_are_named_say_Sindri_memory_stores_and_are_marked_read_or_destructive()
    {
        Assert.Equal(["memory_list", "memory_create", "memory_search", "memory_write", "memory_forget"], MemoryTools.All.Select(static t => t.Name), StringComparer.Ordinal);
        Assert.All(MemoryTools.All, static t => Assert.Contains("Sindri memory", t.Description!, StringComparison.Ordinal));
        Assert.True(MemoryTools.All.Single(static t => t.Name == "memory_search").Annotations!.ReadOnlyHint);
        Assert.True(MemoryTools.All.Single(static t => t.Name == "memory_forget").Annotations!.DestructiveHint);
    }
}
