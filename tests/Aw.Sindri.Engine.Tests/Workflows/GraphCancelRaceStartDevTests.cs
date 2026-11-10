using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows.Spike;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A probe for the parking cancel race (issue 2595): does a cancel sent as soon as a wait node is running also lose to the block's activity on a real dev server
/// ('temporal server start-dev'), or only on the time-skipping test server? It uses the old test shape, with no wait for the workflow's timer, and runs it many times on one server.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class GraphCancelRaceStartDevTests
{
    private const int Iterations = 50;
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_cancel_sent_when_a_wait_node_starts_still_ends_the_run_cancelled_on_a_dev_server()
    {
        var environment = await TemporalServerFixture.Start(ServerKind.Local);
        try
        {
            var attributes = new AddSearchAttributesRequest { Namespace = environment.Client.Options.Namespace };
            attributes.SearchAttributes.Add(SindriTemporal.SearchAttributes.ToDictionary(static a => a.Name, static a => a.Type, StringComparer.Ordinal));
            _ = await environment.Client.OperatorService.AddSearchAttributesAsync(attributes);

            var cancelled = 0;
            var unknownActivity = 0;
            List<int> stuck = [];
            for (var i = 1; i <= Iterations; i++)
            {
                var (ended, hasUnknownActivity, status) = await RunOnce(environment);
                cancelled += ended && status == WorkflowRunStatus.Cancelled ? 1 : 0;
                unknownActivity += hasUnknownActivity ? 1 : 0;
                if (!ended)
                {
                    stuck.Add(i);
                }

                TestContext.Current.SendDiagnosticMessage(string.Create(CultureInfo.InvariantCulture, $"iteration {i}: ended={ended}, ACTIVITY_UNKNOWN={hasUnknownActivity}, status={status}"));
            }

            var summary = string.Create(CultureInfo.InvariantCulture, $"start-dev cancel race: {cancelled}/{Iterations} cancelled, {unknownActivity} with ACTIVITY_UNKNOWN, stuck: [{string.Join(", ", stuck)}]");
            TestContext.Current.SendDiagnosticMessage(summary);
            Assert.True(cancelled == Iterations, summary);
        }
        finally
        {
            await environment.ShutdownAsync();
        }
    }

    // One run on a fresh rig: cancel once the wait node's row says running, then wait up to 30 s for the run to end.
    private static async Task<(bool Ended, bool UnknownActivity, WorkflowRunStatus Status)> RunOnce(WorkflowEnvironment environment)
    {
        await using var rig = GraphRig.StartOn(environment);
        var workflow = await rig.Define(GraphRig.Spec("unlimited",
            [GraphRig.Node("start", "manual"), GraphRig.Node("hold", "wait", config: new JsonObject { ["duration"] = "1d" }), GraphRig.Node("after", "echo")],
            [GraphRig.Edge("start", "hold"), GraphRig.Edge("hold", "after")]));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        await WaitUntilRunning(rig, started.RunId, "hold");

        var handle = rig.Client.GetWorkflowHandle(started.RunId.Value);
        await handle.CancelAsync();
        var ended = true;
        try
        {
            _ = await rig.Final(started.RunId, _patience);
        }
        catch (OperationCanceledException) when (!Token.IsCancellationRequested)
        {
            ended = false;   // the run did not reach a final status within the patience
        }

        var history = await handle.FetchHistoryAsync();
        var unknownActivity = history.Events.Any(static e => e.WorkflowTaskFailedEventAttributes is { } failed
            && ($"{failed.Cause} {failed.Failure}".Contains("ACTIVITY_UNKNOWN", StringComparison.OrdinalIgnoreCase) || $"{failed.Cause} {failed.Failure}".Contains("unknown activity", StringComparison.OrdinalIgnoreCase)));
        return (ended, unknownActivity, (await rig.Runs.Get(started.RunId, Token)).Value.Status);
    }

    private static async Task WaitUntilRunning(GraphRig rig, WorkflowRunId run, string node)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_patience);
        while (!(await rig.NodeStatuses(run)).TryGetValue(node, out var status) || status != NodeRunStatus.Running)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }
}
