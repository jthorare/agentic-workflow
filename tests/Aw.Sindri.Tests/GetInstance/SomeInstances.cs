using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>Several instances, resolved by address or id.</summary>
internal sealed class SomeInstances(params InstanceView[] views) : IInstanceDirectory, IInstanceReader
{
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, Core.Workflow.Ids.RecipeId recipe, CancellationToken cancellationToken) =>
        throw new NotSupportedException("no attempts in this fake");

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceAddress>>(views.FirstOrDefault(v => v.Id.Value == reference || v.Address.Value == reference) is { } found
            ? found.Address
            : Core.Workflow.WorkflowErrors.InstanceNotFound.With($"no instance '{reference}'"));

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceView>>(views.FirstOrDefault(v => v.Address == address) is { } view ? view : Core.Workflow.WorkflowErrors.InstanceNotFound.With($"no instance '{address.Value}'"));
}
