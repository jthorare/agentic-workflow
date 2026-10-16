using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ListOutbox;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine spec rev 8.1 "Phase 6", signal 1, on the time-skipping server: with the dev profile approved
/// ('workflow.effects: [effect.notify.desk]', 'workflow.dispatchDelay: 60s'), 'workflow.dev-notify' on T-4 runs its
/// desk Step; its 'complete' moves 'ask -> sent' with 'effect.notify.desk'. The effect waits out the delay as
/// 'outbox.pending', then its activity dispatches it: 'outbox list --dispatched' shows it, notifications.jsonl has its
/// one line, and the 'notify.delivered' reply, caused by the effect's row, reaches the instance through the bridge and
/// moves it to 'done'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class PhaseSixSignalTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private readonly WorkflowsHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Dev_notify_dispatches_notify_desk_after_the_delay_and_reaches_done_via_notify_delivered()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        var space = env.Client.Options.Namespace;
        await using var sindri = _harness.Sindri(static _ => { }, name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? space : null);   // the bridge signals in the server's namespace
        await WorkflowsHarness.HoldLease(sindri, Token);
        await ApproveDevProfile(sindri);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);   // the bridge signals through control.json's Temporal endpoint
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", "T-4", Token));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var line = _harness.Fact("Hello from workflow.dev-notify.");
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"),
                new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = line }, Tokens: 0, StepRuntime.Desk, new string('f', 64));

            var completed = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            var pending = await RecipeEffect(sindri, EffectState.Pending);
            var early = await Outbox(sindri, EffectState.Dispatched);
            await env.DelayAsync(TimeSpan.FromSeconds(61), Token);
            var dispatched = await RecipeEffect(sindri, EffectState.Dispatched);
            var notices = Notices(dispatched.Identity);
            await Bridge(sindri);
            var result = await Ended(handle);

            Assert.Equal("-> sent", completed.Happened);
            Assert.Equal((handle.Id, CommandOrigin.Recipe), (pending.Instance.Value, pending.Origin));
            Assert.DoesNotContain(early, static c => c.Kind == _notify && c.Origin == CommandOrigin.Recipe);   // the delay hasn't passed
            Assert.Equal(pending.Identity, dispatched.Identity);
            _ = Assert.Single(notices);
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
        }, Token);
    }

    /// <summary>Writes the shipped dev profile as the home's profile and approves it, as 'profile approve' leaves it.</summary>
    private async Task ApproveDevProfile(IServiceProvider sindri)
    {
        var file = Path.Combine(_harness.Home, "profile", "profile.yaml");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);   // why: the path has a directory
        await File.WriteAllTextAsync(file, WorkflowsHarness.Shipped("dev/profile.yaml"), Token);
        var loaded = await sindri.GetRequiredService<IProfileSource>().Load(Token);
        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        var saved = await sindri.GetRequiredService<IApprovals>().Save(loaded.Value, Token);
        Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
    }

    /// <summary>'outbox list' (filtered to <paramref name="state"/>), as the CLI's slice answers it.</summary>
    private static async Task<IReadOnlyList<OutboxCommandView>> Outbox(IServiceProvider sindri, EffectState state)
    {
        await using var scope = sindri.CreateAsyncScope();
        var listed = await scope.ServiceProvider.GetRequiredService<IService<ListOutboxRequest, ListOutboxResponse>>().Handle(new ListOutboxRequest(state), Token);
        Assert.False(listed.IsError, listed.IsError ? listed.Error.Message : null);
        return listed.Value.Commands;
    }

    /// <summary>The recipe's 'effect.notify.desk' in <paramref name="state"/>, read until it is there (the rows follow the workflow task).</summary>
    private static async Task<OutboxCommandView> RecipeEffect(IServiceProvider sindri, EffectState state)
    {
        var deadline = DateTime.UtcNow + _patience;
        while (DateTime.UtcNow < deadline)
        {
            if ((await Outbox(sindri, state)).FirstOrDefault(static c => c.Kind == _notify && c.Origin == CommandOrigin.Recipe) is { } found)
            {
                return found;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail($"outbox list --{state.ToString().ToLowerInvariant()} never showed the recipe's effect.notify.desk");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>The lines of '$AW_SINDRI_HOME/notifications.jsonl' for <paramref name="identity"/>.</summary>
    private string[] Notices(string identity)
    {
        var file = Path.Combine(_harness.Home, "notifications.jsonl");
        Assert.True(File.Exists(file), "notify.desk wrote no notifications.jsonl");
        return [.. File.ReadAllLines(file).Where(l =>
        {
            using var line = JsonDocument.Parse(l);
            return line.RootElement.TryGetProperty("identity", out var id) && id.GetString() == identity;
        })];
    }

    /// <summary>Runs the bridge service once, as the scheduler would, under the test's lease.</summary>
    private static async Task Bridge(IServiceProvider sindri)
    {
        await using var scope = sindri.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity() == EventBridge.ServiceId);
        var grant = sindri.GetRequiredService<HeldLease>().Current ?? throw new InvalidOperationException("the test holds no lease");
        var ran = await bridge.Run(new ScheduledRun(grant, KnownInstants.Epoch), Token);
        Assert.False(ran.IsError, ran.IsError ? ran.Error.Message : null);
    }

    private static async Task<RecipeResult> Ended(WorkflowHandle<RecipeWorkflow, RecipeResult> handle)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(Token);
        patience.CancelAfter(_patience);
        return await handle.GetResultAsync(rpcOptions: new RpcOptions { CancellationToken = patience.Token });
    }
}
