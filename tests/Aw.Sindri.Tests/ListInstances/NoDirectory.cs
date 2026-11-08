using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.ListInstances;

internal sealed class NoDirectory : IInstanceDirectory
{
    public ValueTask<Result<AttemptState?>> Latest(ItemKey item, Core.Workflow.Ids.RecipeId recipe, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a list never resolves a reference here");

    public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
        throw new NotSupportedException("a list never resolves a reference here");
}
