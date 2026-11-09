using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.ReplaceWorkflow;

/// <summary>
/// Saves a whole new graph as the next revision. A base revision that is not the latest is SND-GRAPH-007 with the current revision and nothing is written; a graph that fails the draft checks is
/// findings (SND-GRAPH) and nothing is written. An unknown slug is SND-GRAPH-008, an archived workflow SND-GRAPH-011.
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
/// <param name="log">Writes the lifecycle event.</param>
internal sealed class ReplaceWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker, WorkflowChangeLog log) : IService<ReplaceWorkflowRequest, ReplaceWorkflowResponse>
{
    public async ValueTask<Result<ReplaceWorkflowResponse>> Handle(ReplaceWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var spec = GraphJson.Parse(request.Spec);
        if (spec.IsError)
        {
            return spec.Error;
        }

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

        if (record.ArchivedAt is not null)
        {
            return SindriErrors.GraphArchived.With($"'{request.Slug.Value}' is archived; create a new workflow from its spec to edit it");
        }

        if (request.BaseRevision != record.LatestRevision)
        {
            var current = record.LatestRevision.ToString(CultureInfo.InvariantCulture);
            return SindriErrors.GraphStaleRevision
                .With($"This workflow is at revision {current}, and this replace was made on revision {request.BaseRevision.ToString(CultureInfo.InvariantCulture)}. Read it again (current revision {current}) and retry.")
                .WithExtension("currentRevision", JsonDocument.Parse(current).RootElement.Clone());
        }

        var validator = await checker.Validator(cancellationToken).ConfigureAwait(false);
        var validation = validator.Validate(spec.Value, GraphMode.Draft);
        if (!validation.Passed)
        {
            return SindriErrors.GraphFindings.With(string.Join("; ", validation.Blocking.Select(static f => f.ToString())));
        }

        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var saved = await workflows.Save(record.Id, request.BaseRevision, spec.Value, request.By, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowRevised, new WorkflowChangedPayload(record.Id, record.Slug, saved.Value.Revision, saved.Value.SpecHash, request.By), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new ReplaceWorkflowResponse(record.Slug, saved.Value.Revision, saved.Value.SpecHash);
    }
}
