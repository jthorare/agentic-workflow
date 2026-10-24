using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// Opens a workflow.scoping instance for a read the caller is allowed (scoping spec API2, API9): an unknown instance answers SND-ITEM-404; an instance of another recipe SND-SCOPING-001.
/// </summary>
/// <param name="directory">Resolves the instance id to its address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ScopingInstances(IInstanceDirectory directory, IInstanceReader runtime, ICallerContextAccessor callers)
{
    private const string Recipe = "workflow.scoping";

    /// <summary>The instance's view, when the caller may read it and it runs the scoping recipe.</summary>
    internal async ValueTask<Result<InstanceView>> Open(WorkflowInstanceId instance, CancellationToken cancellationToken)
    {
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a scoping read needs a caller");
        }

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
            : ScopingErrors.NotAScopingInstance.With($"{instance.Value} runs {view.Value.Recipe.Id.Value} (fix: pass the address of a {Recipe} instance)");
    }
}
