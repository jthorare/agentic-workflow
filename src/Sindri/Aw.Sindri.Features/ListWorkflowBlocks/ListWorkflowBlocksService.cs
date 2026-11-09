using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>The block catalog as the picker shows it: grouped by each block's group, each with its title, config fields, outputs, branches and example, all read from the block's own definition.</summary>
/// <param name="catalog">The block types.</param>
internal sealed class ListWorkflowBlocksService(IGraphBlockCatalog catalog) : IService<ListWorkflowBlocksRequest, ListWorkflowBlocksResponse>
{
    private static readonly string[] _order = ["Triggers", "Data", "AI", "Logic", "Outputs"];

    public ValueTask<Result<ListWorkflowBlocksResponse>> Handle(ListWorkflowBlocksRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var groups = catalog.Types.Values
            .GroupBy(static b => b.Group, StringComparer.Ordinal)
            .OrderBy(static g => Rank(g.Key)).ThenBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static g => new BlockGroupView(g.Key, [.. g.OrderBy(static b => b.Title, StringComparer.Ordinal).Select(View)]))
            .ToList();
        return ValueTask.FromResult<Result<ListWorkflowBlocksResponse>>(new ListWorkflowBlocksResponse(groups));
    }

    private static int Rank(string group)
    {
        var at = Array.IndexOf(_order, group);
        return at < 0 ? _order.Length : at;
    }

    private static BlockView View(IGraphBlockType block)
    {
        var example = Example(block.Example);
        var outputs = block.OutputsFor(example as JsonObject ?? new JsonObject());
        return new BlockView(block.Type, block.Title, block.IsTrigger,
            [.. block.Config.Fields.Select(static f => new BlockFieldView(f.Name, Word(f.Type), f.Required))], outputs.Fields, outputs.BranchNames, example);
    }

    private static JsonNode? Example(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: an example that is not JSON is a block bug; the listing still shows the block, without it
        {
            return null;
        }
    }

    private static string Word(BlockFieldType type) => type switch
    {
        BlockFieldType.Text => "text",
        BlockFieldType.Boolean => "boolean",
        BlockFieldType.Number => "number",
        BlockFieldType.TextList => "text list",
        BlockFieldType.Map => "object",
        _ => "value",
    };
}
