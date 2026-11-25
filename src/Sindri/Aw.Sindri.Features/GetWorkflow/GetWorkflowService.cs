using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Schedule;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>
/// One workflow by slug, an archived one too: the revision it runs (the newest when it is off, or the one the request asks for: a number, or the newest), the graph, the hash and who saved the newest revision. Node positions come from
/// <see cref="WorkflowPositions"/>. An unknown slug is SND-GRAPH-008.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="catalog">The block types; each node's summary line comes from its type.</param>
/// <param name="clock">The time the next run is measured from (issue 3756).</param>
internal sealed class GetWorkflowService(IWorkflowStore workflows, IGraphBlockCatalog catalog, TimeProvider clock) : IService<GetWorkflowRequest, GetWorkflowResponse>
{
    public async ValueTask<Result<GetWorkflowResponse>> Handle(GetWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await Find(request.Slug, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value;
        var target = Target(request, record);
        if (target.IsError)
        {
            return target.Error;
        }

        var shown = await workflows.GetRevision(record.Id, target.Value, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return shown.Error;
        }

        var newest = shown.Value.Revision == record.LatestRevision ? shown : await workflows.GetRevision(record.Id, record.LatestRevision, cancellationToken).ConfigureAwait(false);
        if (newest.IsError)
        {
            return newest.Error;
        }

        var running = await Running(record, shown, cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        return new GetWorkflowResponse(record.Id, record.Slug, record.Name, shown.Value.Revision, record.ActiveRevision, record.LatestRevision, record.ArchivedAt, record.CreatedBy,
            newest.Value.CreatedBy, newest.Value.CreatedAt, shown.Value.SpecHash, SpecJson(shown.Value.Spec), WorkflowPositions.Of(shown.Value.Spec), [.. shown.Value.Spec.Nodes.Select(Summary)],
            WorkflowNextRun.Of(record, running.Value.Spec, clock.GetUtcNow()));
    }

    private async ValueTask<Result<WorkflowRecord>> Find(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => r.Slug == slug);
        if (record is null)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{slug.Value}'; list the workflows to see what exists");
        }

        return record;
    }

    // why: triggers fire the ACTIVE revision, so the next run is its schedule's, also when the request shows another revision (the list row says the same)
    private async ValueTask<Result<WorkflowRevision>> Running(WorkflowRecord record, Result<WorkflowRevision> shown, CancellationToken cancellationToken) =>
        record.ActiveRevision is { } active && active != shown.Value.Revision ? await workflows.GetRevision(record.Id, active, cancellationToken).ConfigureAwait(false) : shown;

    // why: GraphJson.Write always writes a JSON object, so Parse never answers null
    private static JsonNode SpecJson(GraphSpec spec) => JsonNode.Parse(GraphJson.Write(spec))!;

    private static Result<int> Target(GetWorkflowRequest request, WorkflowRecord record)
    {
        if (!request.Newest && request.Revision is { } asked && (asked < 1 || asked > record.LatestRevision))
        {
            return SindriErrors.GraphNotFound.With(string.Create(CultureInfo.InvariantCulture, $"workflow '{request.Slug.Value}' has no revision {asked} (its newest is {record.LatestRevision})"));   // never the active spec instead
        }

        return request.Newest ? record.LatestRevision : request.Revision ?? record.ActiveRevision ?? record.LatestRevision;
    }

    private NodeSummary Summary(GraphNode node)
    {
        var line = catalog.Types.TryGetValue(node.Type, out var block) ? block.Summarize(node.Config) : new BlockSummary(node.Type);
        return new NodeSummary(node.Id.Value, line.Text, line.Problem, block?.Title);
    }
}
