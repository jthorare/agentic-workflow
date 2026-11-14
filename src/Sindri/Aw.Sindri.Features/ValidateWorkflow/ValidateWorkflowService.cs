using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Features.ValidateWorkflow;

/// <summary>Runs the validator in activate mode over a stored revision or a given graph and answers the findings. A graph that is not valid JSON is the answer's refusal (SND-GRAPH-001); an unknown workflow or revision is SND-GRAPH-008.</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="checker">Builds the validator.</param>
internal sealed class ValidateWorkflowService(IWorkflowStore workflows, IWorkflowChecker checker) : IService<ValidateWorkflowRequest, ValidateWorkflowResponse>
{
    public async ValueTask<Result<ValidateWorkflowResponse>> Handle(ValidateWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((request.Slug is null) == (request.Spec is null))
        {
            return SindriErrors.InvalidInput.With("give either a workflow slug or a spec, not both and not neither");
        }

        var spec = request.Spec is { } text ? GraphJson.Parse(text) : await Stored(request.Slug!.Value, request.Revision, cancellationToken).ConfigureAwait(false);   // why: the check above leaves Slug set when Spec is null
        if (spec.IsError)
        {
            return spec.Error;
        }

        var validator = await checker.Validator(cancellationToken).ConfigureAwait(false);
        var result = validator.Validate(spec.Value, GraphMode.Activate);
        return new ValidateWorkflowResponse(result.Passed, result.Passed ? GraphJson.Hash(spec.Value) : null,
            [.. result.Findings.Select(static f => WorkflowFinding.Of(f))]);
    }

    private async ValueTask<Result<GraphSpec>> Stored(Core.Graph.Ids.WorkflowSlug slug, int? revision, CancellationToken cancellationToken)
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

        var found = await workflows.GetRevision(record.Id, revision, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value.Spec;
    }
}
