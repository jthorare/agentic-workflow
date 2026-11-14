using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.CreateWorkflow;

/// <summary>
/// Makes a workflow, off, at revision 1, from a spec or a template. The spec must pass the draft checks (an activation check comes with 'activate'); a taken slug is SND-GRAPH-009.
/// The 'workflow.created' event is written after the store accepts the workflow.
/// </summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
/// <param name="ids">Makes the workflow's id.</param>
/// <param name="log">Writes the lifecycle event.</param>
/// <param name="ci">Finds the CI a starter template can read; asked only when the request names a template.</param>
internal sealed class CreateWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker, IIdGenerator ids, WorkflowChangeLog log, ICiSourceLookup ci) : IService<CreateWorkflowRequest, CreateWorkflowResponse>
{
    private static readonly HashSet<string> _reserved = new(["docs", "blocks", "trigger-events"], StringComparer.Ordinal);

    public async ValueTask<Result<CreateWorkflowResponse>> Handle(CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_reserved.Contains(request.Slug.Value))
        {
            // why: GET /sindri/workflows/docs, /blocks and /trigger-events are the doc, block and recent-events routes, so a workflow with that slug could never be read
            return SindriErrors.InvalidInput.With($"'{request.Slug.Value}' is reserved for the workflow docs, blocks and trigger-events routes; pick another slug");
        }

        var spec = await Source(request, cancellationToken).ConfigureAwait(false);
        if (spec.IsError)
        {
            return spec.Error;
        }

        var validator = await checker.Validator(cancellationToken).ConfigureAwait(false);
        var checkedSpec = validator.Validate(spec.Value, GraphMode.Draft);
        if (!checkedSpec.Passed)
        {
            return SindriErrors.GraphFindings.With(string.Join("; ", checkedSpec.Blocking.Select(static f => f.ToString())));
        }

        if (log.LeaseProblem() is { } down)
        {
            return down;
        }

        var id = WorkflowId.New(ids);
        var created = await workflows.Create(id, request.Slug, spec.Value, request.By, cancellationToken).ConfigureAwait(false);
        if (created.IsError)
        {
            return created.Error;
        }

        var recorded = await log.Record(EventTypes.WorkflowCreated, new WorkflowChangedPayload(id, request.Slug, created.Value.Revision, created.Value.SpecHash, request.By), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new CreateWorkflowResponse(id, request.Slug, created.Value.Revision, created.Value.SpecHash);
    }

    private async ValueTask<Result<GraphSpec>> Source(CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        if ((request.Template is null) == (request.Spec is null))
        {
            return SindriErrors.InvalidInput.With("give either a template or a spec, not both and not neither");
        }

        if (request.Spec is { } text)
        {
            return GraphJson.Parse(text);
        }

        var name = request.Template!;   // why: the check above leaves Template set when Spec is null
        var source = GraphTemplates.ReadsCi(name) ? await ci.Find(cancellationToken).ConfigureAwait(false) : null;
        var template = GraphTemplates.Find(name, source);
        return template is null
            ? SindriErrors.InvalidInput.With($"no template '{request.Template}'; the templates are {string.Join(", ", GraphTemplates.All.Select(static t => t.Name))}")
            : template.Spec;
    }
}
