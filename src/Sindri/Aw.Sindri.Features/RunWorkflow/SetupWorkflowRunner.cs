using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>Runs the RunWorkflow command for the setup service, which calls it as 'service:setup' (the catalog row grants that).</summary>
/// <param name="run">The command.</param>
internal sealed class SetupWorkflowRunner(IService<RunWorkflowRequest, RunWorkflowResponse> run) : ISetupWorkflowRunner
{
    public async ValueTask<Result<Unit>> Run(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var started = await run.Handle(new RunWorkflowRequest(slug, Payload: null, "service:setup"), cancellationToken).ConfigureAwait(false);
        return started.IsError ? started.Error : Unit.Value;
    }
}
