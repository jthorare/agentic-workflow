using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The memory endpoints' fencing for a session: the answer as the MCP tool gives it (<see cref="McpFencing"/> with <see cref="FencePlan.MemoryStores"/>, <see cref="FencePlan.MemoryFacts"/> or <see cref="FencePlan.MemoryStore"/>).</summary>
/// <param name="json">The host's JSON options.</param>
internal sealed class MemoryFencing(JsonSerializerOptions json) : IMemoryFencing
{
    public IResult Stores(ListMemoriesResponse stores) => Fenced(AwJson.Serialize(stores, json), FencePlan.MemoryStores);

    public IResult Facts(SearchMemoryResponse recall) => Fenced(AwJson.Serialize(recall, json), FencePlan.MemoryFacts);

    public IResult Store(CreateMemoryResponse created) => Fenced(AwJson.Serialize(created, json), FencePlan.MemoryStore);

    private static Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult Fenced(byte[] body, FencePlan plan)
    {
        using var document = JsonDocument.Parse(body);
        return TypedResults.Text(McpFencing.Apply(document.RootElement, plan).GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }
}
