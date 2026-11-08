using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// No instances at all, for slice harnesses that compose a slice reading instances but never reach one (the registry's
/// IssueChallenge, whose instance forms its own integration tests cover): every lookup is SND-WF-001.
/// </summary>
public sealed class NoInstances : IInstanceDirectory, IInstanceRuntime
{
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceAddress>>(NotFound(reference));

    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no instances in this harness");

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceView>>(NotFound(address.Value));

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CommandResult>>(NotFound(address.Value));

    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CommandResult>>(NotFound(address.Value));

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(NotFound(address.Value));

    private static Error NotFound(string reference) => WorkflowErrors.InstanceNotFound.With($"no instance {reference} in this harness");
}
