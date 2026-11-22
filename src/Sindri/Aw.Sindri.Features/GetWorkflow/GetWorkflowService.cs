using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>
/// One workflow by slug, an archived one too: the revision it runs (the newest when it is off, or the one the request asks for: a number, or the newest), the graph, the hash and who saved the newest revision. Node positions come from
/// <see cref="WorkflowPositions"/>. An unknown slug is SND-GRAPH-008.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="catalog">The block types; each node's summary line comes from its type.</param>
internal sealed class GetWorkflowService(IWorkflowStore workflows, IGraphBlockCatalog catalog) : IService<GetWorkflowRequest, GetWorkflowResponse>
{
    public async ValueTask<Result<GetWorkflowResponse>> Handle(GetWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => r.Slug == request.Slug);
        if (record is null)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{request.Slug.Value}'; list the workflows to see what exists");
        }

        if (!request.Newest && request.Revision is { } asked && (asked < 1 || asked > record.LatestRevision))
        {
            return SindriErrors.GraphNotFound.With($"workflow '{request.Slug.Value}' has no revision {asked} (its newest is {record.LatestRevision})");   // never the active spec instead
        }

        var target = request.Newest ? record.LatestRevision : request.Revision ?? record.ActiveRevision ?? record.LatestRevision;
        var shown = await workflows.GetRevision(record.Id, target, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return shown.Error;
        }

        var newest = shown.Value.Revision == record.LatestRevision ? shown : await workflows.GetRevision(record.Id, record.LatestRevision, cancellationToken).ConfigureAwait(false);
        if (newest.IsError)
        {
            return newest.Error;
        }

        // why: GraphJson.Write always writes a JSON object, so Parse never answers null
        var spec = JsonNode.Parse(GraphJson.Write(shown.Value.Spec))!;
        return new GetWorkflowResponse(record.Id, record.Slug, record.Name, shown.Value.Revision, record.ActiveRevision, record.LatestRevision, record.ArchivedAt, record.CreatedBy,
            newest.Value.CreatedBy, newest.Value.CreatedAt, shown.Value.SpecHash, spec, WorkflowPositions.Of(shown.Value.Spec), [.. shown.Value.Spec.Nodes.Select(Summary)]);
    }

    private NodeSummary Summary(GraphNode node)
    {
        var line = catalog.Types.TryGetValue(node.Type, out var block) ? block.Summarize(node.Config) : new BlockSummary(node.Type);
        return new NodeSummary(node.Id.Value, line.Text, line.Problem, block?.Title);
    }
}
