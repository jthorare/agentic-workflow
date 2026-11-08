using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// One live instance, for slice tests of the reads that resolve a reference and ask the instance for its view. Like the
/// Temporal directory, a reference that is an address resolves as given (whether or not an instance has it), a 'WFI-…'
/// id or a bare item resolves only to the instance this fake holds, and a query for any other address is SND-WF-001.
/// </summary>
/// <param name="view">The instance it holds.</param>
public sealed class OneInstance(InstanceView view) : IInstanceDirectory, IInstanceRuntime
{
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no attempts in this fake");

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var item = view.Address.Value[..view.Address.Value.IndexOf('/', StringComparison.Ordinal)];
        Result<InstanceAddress> resolved = InstanceAddress.TryFrom(reference, out var address) ? address
            : reference == view.Id.Value || reference == item ? view.Address
            : NotFound(reference);
        return ValueTask.FromResult(resolved);
    }

    public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no starts in this fake");

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceView>>(address == view.Address ? view : NotFound(address.Value));

    public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no commands in this fake");

    public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no open-requests in this fake");

    public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no signals in this fake");

    private static Error NotFound(string reference) => WorkflowErrors.InstanceNotFound.With($"no instance {reference} (fix: sindri workflow list)");
}
