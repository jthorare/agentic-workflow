using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ListOutbox;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// 'effect.notify.desk' and the engine's notices end to end on the time-skipping server (engine spec §5.7, §6.3, §12;
/// engine P6 unit (c)), with the real block, the notifications file in the test's home and a fake OS notification
/// centre. The shipped recheck runs against an approved profile that allowlists 'effect.notify.desk', so the
/// dispatch delay is the default.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeskNotificationsTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(20);
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private readonly WorkflowsHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    /// <summary>The approved profile allowlists 'effect.notify.desk' (default dispatch delay).</summary>
    private static void Allowing(IServiceCollection services)
    {
        var profiles = new InMemoryProfiles();
        var profile = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Workflow: new WorkflowSettings(Effects: [_notify.Value], DispatchDelay: null));
        Assert.False(profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), Token).AsTask().GetAwaiter().GetResult().IsError);
        _ = services.AddSingleton<IApprovedProfile>(profiles);
    }

    /// <summary>Phase 6 signal 1 as far as unit (c) goes: the recipe's notice is dispatched after the delay, written once, and its delivery ends the instance.</summary>
    [Fact]
    public async Task Dev_notify_writes_its_line_after_the_delay_and_reaches_done_via_notify_delivered()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        var space = env.Client.Options.Namespace;
        await using var sindri = _harness.Sindri(Allowing, name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? space : null);   // the bridge signals in the server's namespace
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", "T-4", Token));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"),
                new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = _harness.Fact("Hello.") }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            var pending = await Effect(sindri, EffectState.Pending, CommandOrigin.Recipe);
            await env.DelayAsync(EffectSettings.DefaultDispatchDelay + TimeSpan.FromSeconds(1), Token);
            var dispatched = await Effect(sindri, EffectState.Dispatched, CommandOrigin.Recipe);
            var firstTask = await Effect(sindri, EffectState.Dispatched, CommandOrigin.Engine);
            await Bridge(sindri);
            var result = await Ended(handle);

            Assert.Equal(pending.Identity, dispatched.Identity);
            var line = Assert.Single(Lines(dispatched.Identity));
            Assert.Equal(("info", handle.Id, "T-4"), (line.GetProperty("class").GetString(), line.GetProperty("instance").GetString(), line.GetProperty("item").GetString()));
            Assert.Equal("Waiting for the desk notification to be delivered.", line.GetProperty("text").GetString());
            Assert.Equal("interrupt", Assert.Single(Lines(firstTask.Identity)).GetProperty("class").GetString());
            Assert.DoesNotContain(handle.Id, Assert.Single(_harness.Notifier.Shown).Title, StringComparison.Ordinal);   // only the first desk task interrupts; its title is the item's, never the instance id
            Assert.Equal(("done", "resolved"), (result.State, result.Outcome));
        }, Token);
    }

    /// <summary>The first desk task interrupts at once, and the desk timeout's park interrupts again.</summary>
    [Fact]
    public async Task The_first_desk_task_and_a_park_each_write_an_interrupt_line()
    {
        var env = server.Environment;
        await using var sindri = _harness.Sindri(Allowing);
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-8", Token));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var task = await Effect(sindri, EffectState.Dispatched, CommandOrigin.Engine);
            await env.DelayAsync(TimeSpan.FromDays(3), Token);
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            var notices = await Notices(count: 2);

            Assert.Equal(task.Identity, notices[0].GetProperty("identity").GetString());
            Assert.All(notices, static n => Assert.Equal("interrupt", n.GetProperty("class").GetString()));
            var park = notices[1].GetProperty("text").GetString();
            Assert.Contains("stopped, it timed out", park, StringComparison.Ordinal);
            Assert.DoesNotContain("parked (timeout)", park, StringComparison.Ordinal);
            Assert.Equal(2, _harness.Notifier.Shown.Count);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>The effect of <paramref name="origin"/> in <paramref name="state"/>, read through 'outbox list' until it is there.</summary>
    private static async Task<OutboxCommandView> Effect(IServiceProvider sindri, EffectState state, CommandOrigin origin)
    {
        var deadline = RealTime.NowDateTime + _patience;
        while (RealTime.NowDateTime < deadline)
        {
            await using var scope = sindri.CreateAsyncScope();
            var listed = await scope.ServiceProvider.GetRequiredService<IService<ListOutboxRequest, ListOutboxResponse>>().Handle(new ListOutboxRequest(state), Token);
            Assert.False(listed.IsError, listed.IsError ? listed.Error.Message : null);
            if (listed.Value.Commands.FirstOrDefault(c => c.Kind == _notify && c.Origin == origin) is { } found)
            {
                return found;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail($"outbox list never showed a {origin} effect.notify.desk {state}");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>The lines of notifications.jsonl, read until there are <paramref name="count"/>.</summary>
    private async Task<IReadOnlyList<JsonElement>> Notices(int count)
    {
        var deadline = RealTime.NowDateTime + _patience;
        while (RealTime.NowDateTime < deadline)
        {
            if (All() is { } lines && lines.Count >= count)
            {
                return lines;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"notifications.jsonl never had {count} lines"));
        return [];
    }

    private IReadOnlyList<JsonElement> Lines(string identity) => [.. (All() ?? []).Where(l => l.GetProperty("identity").GetString() == identity)];

    private List<JsonElement>? All()
    {
        var file = Path.Combine(_harness.Home, "notifications.jsonl");
        return File.Exists(file) ? [.. File.ReadAllLines(file).Select(static l => JsonDocument.Parse(l).RootElement.Clone())] : null;
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
