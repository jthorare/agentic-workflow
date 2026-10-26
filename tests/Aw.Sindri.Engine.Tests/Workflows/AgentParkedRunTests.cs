using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A run the state timeout parks (engine P8 U3): the park keeps the session, and a cancel stops it. The state timeout is at least a minute and time skipping
/// doesn't advance while the watch runs, so these take about two minutes of real time each (the timeout retries once before it parks, owner q93): the heavy lane, out of the integration lane's 120 s budget.
/// </summary>
[Lane(TestLane.Heavy)]
[Collection(nameof(WallClockAlone))]
public sealed class AgentParkedRunTests(SindriTimeSkippingServer skipping) : AgentRuntimeTestBase, IClassFixture<SindriTimeSkippingServer>
{
    [Fact(Timeout = 300_000)]
    public async Task The_state_timeout_parks_and_the_session_stays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct, skipping.Environment);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);

            await SkipToPark(handle, ct);
            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.Equal((0, 1), (_launcher.Stops.Count, _launcher.SessionCount));   // a park keeps the session
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task A_cancel_of_a_parked_run_stops_the_session_the_park_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct, skipping.Environment);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            await SkipToPark(handle, ct);
            _ = await WorkflowsHarness.Until(handle, static v => v.Question is not null);
            Assert.Empty(_launcher.Stops);

            _ = await Cancel(handle, 'd');

            await WaitFor(() => _launcher.Stops.Contains(rig.Launch), ct);
            Assert.Equal(0, _launcher.SessionCount);
        });
    }

    /// <summary>The first 1m timeout retries the state (owner q93) and the re-armed one parks: skips a minute and a bit at a time until the park shows, at most four times.</summary>
    private async Task SkipToPark(WorkflowHandle<RecipeWorkflow, RecipeResult> handle, CancellationToken ct)
    {
        for (var i = 0; i < 4; i++)
        {
            await WorkflowsHarness.Skip(skipping.Environment, TimeSpan.FromSeconds(70), ct);
            if ((await handle.QueryAsync(wf => wf.GetInstance())).Question is not null)
            {
                return;
            }
        }
    }
}
