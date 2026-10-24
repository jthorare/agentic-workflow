using Aw.Kernel;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstanceLine;

/// <summary>Opens a workflow.line instance for the board read (line spec LN-API1). Every caller reads the same: an unknown instance answers SND-ITEM-404, an instance of another recipe SND-LINE-003.</summary>
/// <param name="directory">Resolves the instance id to its address.</param>
/// <param name="runtime">The instances on Temporal.</param>
internal sealed class LineInstances(IInstanceDirectory directory, IInstanceReader runtime)
{
    private const string Recipe = "workflow.line";

    internal async ValueTask<Result<InstanceView>> Open(WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        var address = await directory.Resolve(instance.Value, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return InstanceScope.Unknown(address.Error, instance.Value);
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return InstanceScope.Unknown(view.Error, instance.Value);
        }

        return string.Equals(view.Value.Recipe.Id.Value, Recipe, StringComparison.Ordinal)
            ? view.Value
            : LineErrors.WrongRecipe.With($"{instance.Value} runs {view.Value.Recipe.Id.Value} (fix: pass the address of a {Recipe} instance)");
    }
}
