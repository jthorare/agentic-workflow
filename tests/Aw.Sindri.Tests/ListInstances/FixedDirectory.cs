using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>A directory that resolves every reference to the one address.</summary>
internal sealed class FixedDirectory(string address) : IInstanceDirectory
{
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, Core.Workflow.Ids.RecipeId recipe, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a list never asks for the latest attempt here");

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceAddress>>(InstanceAddress.From(address));
}
