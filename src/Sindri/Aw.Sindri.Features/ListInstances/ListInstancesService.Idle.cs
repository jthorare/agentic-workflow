using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ListInstances;

internal sealed partial class ListInstancesService
{
    /// <summary>The addresses of the instances whose current agent launch ended as idle with no report (issue #1265), read by key from the 'launches' view; a launch that can't be read is not idle.</summary>
    private async ValueTask<HashSet<string>> IdleAgents(List<InstanceView> views, CancellationToken cancellationToken)
    {
        var idle = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in views.Where(static v => v.Outcome is null))
        {
            if (view.AgentRun?.Launch is not { } launchId)
            {
                continue;
            }

            var launch = await itemViews.Get<LaunchRow>(ViewNames.Launches, launchId, cancellationToken).ConfigureAwait(false);
            if (launch.IsSuccess && launch.Value is { Ended.Reason: SessionEndReason.Idle })
            {
                _ = idle.Add(view.Address.Value);
            }
        }

        return idle;
    }
}
