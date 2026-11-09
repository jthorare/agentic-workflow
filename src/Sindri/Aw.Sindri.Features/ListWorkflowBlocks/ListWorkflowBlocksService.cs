using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;

namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>The block catalog as the picker shows it, from <see cref="GraphBlockListing"/>: grouped by each block's group, each with its title, config fields, outputs, branches and example config.</summary>
/// <param name="catalog">The block types.</param>
/// <param name="docs">The docs; a block's page holds its example spec.</param>
internal sealed class ListWorkflowBlocksService(IGraphBlockCatalog catalog, IWorkflowDocs docs) : IService<ListWorkflowBlocksRequest, ListWorkflowBlocksResponse>
{
    public ValueTask<Result<ListWorkflowBlocksResponse>> Handle(ListWorkflowBlocksRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var groups = GraphBlockListing.Group(catalog, docs.Pages)
            .Select(static g => new BlockGroupView(g.Group.ToString(), [.. g.Blocks.Select(View)]))
            .ToList();
        return ValueTask.FromResult<Result<ListWorkflowBlocksResponse>>(new ListWorkflowBlocksResponse(groups));
    }

    private static BlockView View(GraphBlockEntry entry) =>
        new(entry.Type, entry.Title, entry.IsTrigger,
            [.. entry.Config.Fields.Select(static f => new BlockFieldView(f.Name, Word(f.Type), f.Required))], entry.Outputs.Fields, entry.Outputs.BranchNames, Json(entry.ExampleConfig), Json(entry.ExampleSpec));

    private static JsonNode? Json(string? text)
    {
        if (text is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: an example that is not JSON is a block or doc bug; the listing still shows the block, without it
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
