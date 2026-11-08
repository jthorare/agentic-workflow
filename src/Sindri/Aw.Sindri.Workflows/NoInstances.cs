using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The instance ports with no instance behind them. The recipe workflow is gone, so no workflow instance exists to read, list or command: every read answers
/// 'not found' or an empty list, and starting one is refused. The sweeps and reads that still ask (the lost-session sweep, launch picks, the Linear item links) run
/// over it unchanged until the graph runs (issue 2258 PR 6, 7) give them node runs to read.
/// </summary>
internal sealed class NoInstances : IInstanceDirectory, IInstanceRuntime, IWorkflowInstances
{
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceAddress>>(NotFound(reference));

    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceView>>(SindriErrors.GraphRunControlsPending.With("workflow instances are gone; runs return with the graph engine"));

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceView>>(NotFound(address.Value));

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CommandResult>>(NotFound(address.Value));

    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CommandResult>>(NotFound(address.Value));

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(NotFound(address.Value));

    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<InstanceRow>>([]));

    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<InstanceRow>>([]));

    private static Error NotFound(string reference) => WorkflowErrors.InstanceNotFound.With($"no instance {reference}: workflow instances are gone");
}
