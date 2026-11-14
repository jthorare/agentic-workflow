using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.RunWorkflow;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflows/{slug}/runs ('sindri wf run'): starts a run of the workflow's active revision by hand, or says it was queued or skipped.</summary>
internal static class RunWorkflowEndpoint
{
    internal static RouteGroupBuilder MapRunWorkflow(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/{slug}/runs", static async (string slug, RunWorkflowBody? body, IService<RunWorkflowRequest, RunWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            if (!WorkflowSlug.TryFrom(slug, out var parsed))
            {
                return ((Result<RunWorkflowResponse>)SindriErrors.InvalidInput.With($"'{slug}' is not a workflow slug (lowercase letters, digits and '-')")).ToHttpResult();
            }

            var by = callers.Current?.Actor ?? "owner";
            if (by == "owner" && string.Equals(body?.Source, "cli", StringComparison.Ordinal))
            {
                by = "cli";   // why: the run list tells a CLI start from a button press (#3683); a session never takes the name
            }

            return (await service.Handle(new RunWorkflowRequest(parsed, body?.Payload, by), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
