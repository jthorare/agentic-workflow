using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// The 'forge-reply-facts' marker (forge wiring brief item 3) gates the reply facts and changes no history that ran an effect
/// before it: a history recorded by the build before the marker (a recipe effect dispatched, its reply delivered) replays with no
/// non-determinism, and a history this build records holds the marker and replays too.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EffectReplayTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Marker = "forge-reply-facts";
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeDeskEffect _desk = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_history_that_dispatched_an_effect_before_the_marker_replays_with_no_reply_facts()
    {
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/effect-pre-forge-reply-facts.json")!;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(Token);

        var replayed = await Replay(WorkflowHistory.FromJson("T-72/workflow.dev-notify/1", json));

        Assert.Null(replayed.ReplayFailure);
    }

    /// <summary>The engine now stamps the trigger's actor into an effect's 'with:' arguments; that changes an activity's input only, which replay does not compare, so no patch marks it.</summary>
    [Fact]
    public async Task A_history_recorded_before_the_actor_stamp_replays_with_no_non_determinism()
    {
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/effect-pre-forge-reply-facts.json")!;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(Token);

        var replayed = await Replay(WorkflowHistory.FromJson("T-72/workflow.dev-notify/1", json));

        Assert.Null(replayed.ReplayFailure);
    }

    [Fact]
    public async Task A_history_this_build_records_holds_the_marker_at_the_first_effect_and_replays()
    {
        var env = server.Environment;
        _harness.Recipe("workflow.dev-notify", WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        var profile = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Workflow: new WorkflowSettings(Effects: ["effect.notify.desk"]));
        _ = await _profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), Token);
        await using var sindri = _harness.Sindri(services =>
        {
            _desk.Replace(services);
            _ = services.AddSingleton<IApprovedProfile>(_profiles);
        });
        await WorkflowsHarness.HoldLease(sindri, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", "T-72", Token));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"),
                new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = _harness.Fact("Hello.") }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            await Armed(handle);
            await env.DelayAsync(EffectSettings.DefaultDispatchDelay + TimeSpan.FromSeconds(1), Token);
            _ = Assert.Single(await EffectRows.Eventually(sindri, OutboxEvents.Dispatched, CommandOrigin.Recipe));

            var history = await handle.FetchHistoryAsync();
            var marked = history.Events.Count(static e => e.MarkerRecordedEventAttributes is { } marker
                && marker.Details.Values.Any(static v => v.Payloads_.Any(static p => p.Data.ToStringUtf8().Contains(Marker, StringComparison.Ordinal))));
            var replayed = await Replay(history);

            Assert.Null(replayed.ReplayFailure);
            Assert.Equal(1, marked);
            await handle.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>Waits until the instance's history has the dispatch delay's timer (it follows the pending row's local activity).</summary>
    private static async Task Armed(WorkflowHandle handle)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        while (RealTime.NowDateTime < deadline)
        {
            if ((await handle.FetchHistoryAsync()).Events.Any(static e => e.EventType == Temporalio.Api.Enums.V1.EventType.TimerStarted && e.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan() == EffectSettings.DefaultDispatchDelay))
            {
                return;
            }

            await Task.Delay(100, Token);
        }

        Assert.Fail("the dispatch delay's timer was never started");
    }

    private static Task<WorkflowReplayResult> Replay(WorkflowHistory history) =>
        new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, Token);
}
