using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.RerunWorkflowRun;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The rig's re-run side (#3204): the real 'Re-run from here' service over the rig's stores and starter, so the walk restores the steps it takes.</summary>
internal sealed partial class GraphRig
{
    /// <summary>Re-runs an ended run from one step, as 'sindri wf rerun' does.</summary>
    /// <param name="source">The ended run.</param>
    /// <param name="from">The step to run first again.</param>
    /// <returns>The service's answer.</returns>
    public async Task<RerunWorkflowRunResponse> Rerun(WorkflowRunId source, string from) =>
        (await new RerunWorkflowRunService(Workflows, Runs, _starter).Handle(new RerunWorkflowRunRequest(source, from, Input: null, "owner"), TestContext.Current.CancellationToken)).Value;
}
