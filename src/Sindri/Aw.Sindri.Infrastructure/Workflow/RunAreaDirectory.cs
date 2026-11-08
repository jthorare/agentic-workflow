using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Where an instance's run area is: $AW_SINDRI_HOME/workflow/runs/&lt;WFI-id&gt;/. The one path rule for the packet store and the run-area port.</summary>
internal static class RunAreaDirectory
{
    /// <summary>The run area of an instance.</summary>
    /// <param name="home">Sindri's home.</param>
    /// <param name="instance">The instance's id.</param>
    /// <returns>The directory's path.</returns>
    internal static string Of(string home, WorkflowInstanceId instance) => Path.Combine(home, "workflow", "runs", instance.Value);
}
