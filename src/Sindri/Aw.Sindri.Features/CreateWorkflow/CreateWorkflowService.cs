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
internal sealed class CreateWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker, IIdGenerator ids, WorkflowChangeLog log) : IService<CreateWorkflowRequest, CreateWorkflowResponse>
{
    private static readonly HashSet<string> _reserved = new(["docs", "blocks"], StringComparer.Ordinal);

    /// <summary>Names the profile's first repo for a data template's agent step; null leaves the template as it is (#3509, stub: not read yet).</summary>
    public IFirstRepo? FirstRepo { get; init; }

    public async ValueTask<Result<CreateWorkflowResponse>> Handle(CreateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_reserved.Contains(request.Slug.Value))
        {
            // why: GET /sindri/workflows/docs and /blocks are the doc and block routes, so a workflow with that slug could never be read
            return SindriErrors.InvalidInput.With($"'{request.Slug.Value}' is reserved for the workflow docs and blocks routes; pick another slug");
        }

        var spec = Source(request);
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

    private static Result<GraphSpec> Source(CreateWorkflowRequest request)
    {
        if ((request.Template is null) == (request.Spec is null))
        {
            return SindriErrors.InvalidInput.With("give either a template or a spec, not both and not neither");
        }

        if (request.Spec is { } text)
        {
            return GraphJson.Parse(text);
        }

        var template = GraphTemplates.Find(request.Template!);   // why: the check above leaves Template set when Spec is null
        return template is null
            ? SindriErrors.InvalidInput.With($"no template '{request.Template}'; the templates are {string.Join(", ", GraphTemplates.All.Select(static t => t.Name))}")
            : template.Spec;
    }
}
