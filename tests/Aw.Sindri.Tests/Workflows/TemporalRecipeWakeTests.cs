using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The registry's wake over a real Temporal dev server (engine spec §4.1, W11; engine P6 integration): it finds the open
/// instances whose 'RecipeHash' is the notice's and signals only those, finds nothing when no serve records a Temporal,
/// skips an instance that ended between the list and the signal, and tries every instance before it reports a failure.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalRecipeWakeTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    /// <summary>One dev server for the cases that need one: the hash filter and Running, a vanished instance, and a failing signal.</summary>
    [Fact]
    public async Task Wake_signals_only_the_matching_hash_skips_a_vanished_instance_and_reports_failures_after_trying_every_one()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: Token);
        var notify = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        var timeout = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = Sindri(env);
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var (a, b) = await Started(sindri, env, "T-201", "T-202");
            var frontend = sindri.GetRequiredService<TemporalFrontend>();
            var wake = new TemporalRecipeWake(frontend);
            var drifted = new RecipeWakeNotice(notify.Hash, RecipeWakeKind.Drifted, Sequence: 5, "human:cli");

            // The RecipeHash filter and Running(): two instances on different hashes, only the matching one listed and woken.
            Assert.Equal([InstanceIdOf(a)], await Listed(wake, notify.Hash));   // visibility lists them before the wake looks (it finds instances through the visibility store)
            Assert.Equal([InstanceIdOf(b)], await Listed(wake, timeout.Hash));
            var woken = await wake.Wake(drifted, Token);
            Assert.True(woken.IsSuccess);
            Assert.Equal((1, 0), (await Signals(a), await Signals(b)));

            // An instance listed as open that ended before its signal (Temporal: not found) is skipped; the others still wake.
            var gone = InstanceAddress.Root(ItemKey.From("T-999"), RecipeId.From("workflow.dev-notify"), 1);
            var skipped = await new ListingWake(frontend, [gone, InstanceAddress.From(a.Id)]).Wake(drifted with { Sequence = 6 }, Token);
            Assert.True(skipped.IsSuccess, skipped.IsError ? skipped.Error.Message : null);
            Assert.Equal(2, await Signals(a, atLeast: 2));

            // A signal that fails for any other reason doesn't abandon the instances after it: all are tried, then one error names the failures.
            var failing = new ListingWake(frontend, [InstanceAddress.From(a.Id), InstanceAddress.From(b.Id)], _ => throw new RpcException(RpcException.StatusCode.Unavailable, "the frontend is down", rawStatus: null));
            var failed = await failing.Wake(drifted with { Sequence = 7 }, Token);
            Assert.True(failed.IsError);
            Assert.Contains(a.Id, failed.Error.Message, StringComparison.Ordinal);
            Assert.Contains(b.Id, failed.Error.Message, StringComparison.Ordinal);
            Assert.Equal(2, failing.Tried);
            await AssertFixes(failing, drifted);

            await Task.WhenAll(a.TerminateAsync("test over"), b.TerminateAsync("test over"));
        }, Token);
    }

    private static async Task AssertFixes(TemporalRecipeWake failing, RecipeWakeNotice drifted)
    {
        await AssertApprovalFixIsRetry(failing, drifted);
        await AssertRevokeFixIsHonest(failing, drifted);
    }

    /// <summary>The error's fix works: an approval that missed an instance is retried by approving again, which sends the wake again.</summary>
    private static async Task AssertApprovalFixIsRetry(TemporalRecipeWake failing, RecipeWakeNotice drifted)
    {
        var missed = await failing.Wake(drifted with { Kind = RecipeWakeKind.Approved, Sequence = 8 }, Token);
        Assert.Contains("sindri recipe approve", missed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("again", missed.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>A revoke can be rerun while a cascade is unfinished (the planner refuses only when nothing is left), so its fix says to run it again, and that a missed instance is refused and parks at its next step.</summary>
    private static async Task AssertRevokeFixIsHonest(TemporalRecipeWake failing, RecipeWakeNotice drifted)
    {
        var missed = await failing.Wake(drifted with { Kind = RecipeWakeKind.Revoked, Sequence = 9 }, Token);
        Assert.Contains("SND-RECIPE-005", missed.Error.Message, StringComparison.Ordinal);
        Assert.Contains($"the revocation of {drifted.Hash.Value} is recorded", missed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("send the wake again to what it reaches", missed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("is recorded; run the revoke again to finish any cascade it didn't reach and send the wake again", missed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("refused SND-RECIPE-005 when nothing is left", missed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("parks then", missed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_serve_recording_a_temporal_there_is_nobody_to_wake_and_the_wake_succeeds()
    {
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await using var sindri = _harness.Sindri();   // no control.json: the connect is refused
        var wake = new TemporalRecipeWake(sindri.GetRequiredService<TemporalFrontend>());
        var hash = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml")).Hash;

        var running = await wake.Running(hash, Token);
        var woken = await wake.Wake(new RecipeWakeNotice(hash, RecipeWakeKind.Revoked, Sequence: 6, "human:cli"), Token);

        Assert.True(running.IsSuccess);
        Assert.Empty(running.Value);
        Assert.True(woken.IsSuccess);
    }

    private ServiceProvider Sindri(WorkflowEnvironment env) => _harness.Sindri(
        static _ => { },
        name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? env.Client.Options.Namespace : null);

    /// <summary>Starts 'workflow.dev-notify' on <paramref name="itemA"/> and 'workflow.dev-timeout' on <paramref name="itemB"/>, and waits until the worker has published each one's 'RecipeHash'.</summary>
    private static async Task<(WorkflowHandle<RecipeWorkflow, RecipeResult> A, WorkflowHandle<RecipeWorkflow, RecipeResult> B)> Started(
        IServiceProvider sindri, WorkflowEnvironment env, string itemA, string itemB)
    {
        var a = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", itemA, Token));
        var b = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", itemB, Token));
        _ = await WorkflowsHarness.Until(a, static v => v.Task is not null);
        _ = await WorkflowsHarness.Until(b, static v => v.Task is not null);
        return (a, b);
    }

    /// <summary>The ids <see cref="IRecipeWake.Running"/> lists, polled until the visibility store has them.</summary>
    private static async Task<string[]> Listed(TemporalRecipeWake wake, RecipeHash hash)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            var running = await wake.Running(hash, Token);
            Assert.True(running.IsSuccess);
            if (running.Value.Count > 0 || DateTime.UtcNow > deadline)
            {
                return [.. running.Value.Select(static i => i.Value)];
            }

            await Task.Delay(100, Token);
        }
    }

    private static string InstanceIdOf(WorkflowHandle handle) => WorkflowInstanceId.Of(InstanceAddress.From(handle.Id)).Value;

    /// <summary>The 'wake' signals in the instance's history, polled until there are <paramref name="atLeast"/> (history is read from the visible store).</summary>
    private static async Task<int> Signals(WorkflowHandle handle, int atLeast = 0)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            var signals = (await handle.FetchHistoryAsync()).Events.Count(static e => e.EventType == EventType.WorkflowExecutionSignaled && e.WorkflowExecutionSignaledEventAttributes.SignalName == "wake");
            if (signals >= atLeast || DateTime.UtcNow > deadline)
            {
                return signals;
            }

            await Task.Delay(100, Token);
        }
    }

    /// <summary>The wake with a list the test controls (instead of the visibility store's) and, when given, a signal that fails.</summary>
    private sealed class ListingWake(TemporalFrontend frontend, IReadOnlyList<InstanceAddress> listed, Action<InstanceAddress>? signal = null) : TemporalRecipeWake(frontend)
    {
        private int _tried;

        /// <summary>The signals attempted.</summary>
        public int Tried => _tried;

        internal override ValueTask<IReadOnlyList<InstanceAddress>> Open(RecipeHash hash, CancellationToken cancellationToken) => ValueTask.FromResult(listed);

        internal override Task Signal(ITemporalClient client, InstanceAddress address, RecipeWake wake, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _tried);
            signal?.Invoke(address);
            return signal is null ? base.Signal(client, address, wake, cancellationToken) : Task.CompletedTask;
        }
    }
}
