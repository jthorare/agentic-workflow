using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The rig's test-run side (P4a, spec 2026-11-12 §3.2): the write ports that count writes, and a trigger that asks for a test run.</summary>
internal sealed partial class GraphRig
{
    /// <summary>The notify and create-item ports: what a live run would have written.</summary>
    public GraphTestPorts Ports { get; } = new();

    /// <summary>Asks the starter for a test run of revision 1.</summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="occurrence">The run's number.</param>
    /// <param name="trigger">The trigger payload as JSON.</param>
    /// <returns>The start.</returns>
    public async Task<GraphStart> TriggerTest((WorkflowId Id, WorkflowSlug Slug) workflow, int occurrence, string trigger = """{"kind":"manual"}""") =>
        (await _starter.Start(new GraphStartRequest(workflow.Id, workflow.Slug, Revision: 1, occurrence, trigger, GraphConcurrency.Unlimited, WorkflowRunMode.Test), TestContext.Current.CancellationToken)).Value;
}
