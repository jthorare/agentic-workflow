using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ActivateWorkflow;

/// <summary>Runs the ActivateWorkflow command for the setup service, which calls it as 'service:setup' (the catalog row grants that).</summary>
/// <param name="activate">The command.</param>
internal sealed class SetupWorkflowActivator(IService<ActivateWorkflowRequest, ActivateWorkflowResponse> activate) : ISetupWorkflowActivator
{
    public async ValueTask<Result<Unit>> Activate(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var made = await activate.Handle(new ActivateWorkflowRequest(slug, Revision: null, "service:setup"), cancellationToken).ConfigureAwait(false);
        return made.IsError ? made.Error : Unit.Value;
    }
}
