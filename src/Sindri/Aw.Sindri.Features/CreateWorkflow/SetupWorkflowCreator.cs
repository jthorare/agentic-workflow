using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.CreateWorkflow;

/// <summary>Runs the CreateWorkflow command for the setup service, which calls it as 'service:setup' (the catalog row grants that).</summary>
/// <param name="create">The command.</param>
internal sealed class SetupWorkflowCreator(IService<CreateWorkflowRequest, CreateWorkflowResponse> create) : ISetupWorkflowCreator
{
    public async ValueTask<Result<Unit>> Create(WorkflowSlug slug, string spec, CancellationToken cancellationToken)
    {
        var made = await create.Handle(new CreateWorkflowRequest(slug, Template: null, spec, "service:setup"), cancellationToken).ConfigureAwait(false);
        return made.IsError ? made.Error : Unit.Value;
    }
}
