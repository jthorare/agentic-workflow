using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine spec rev 8.1 "Phase 5", the signal's three cases on the time-skipping server (§5.5, §3.8, X58; verification
/// plan journeys 1–4 and 9), with the dev profile's 'workflow.deskTimeout: 1m': 'workflow.dev-timeout' on T-2 parks
/// 'timeout' once its minute runs out; a second instance on T-8, taken over at 20 s, shows 'taken-over' with its timer
/// paused at 40 s left, nothing fires while it stays taken over, and after 'handback' it parks after roughly the 40 s
/// that remained; 'answer --resume' puts T-2 back in its desk task with the same run token. The signal's last sentence, "under
/// a second of wall clock", is measured by the Prover; this test bounds it at 10 s (Until polls every 100 ms and other
/// assemblies run in parallel) and writes the measured time to the test output.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class PhaseFiveSignalTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly TimeSpan _slack = TimeSpan.FromSeconds(5);
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Workflow_dev_timeout_parks_timeout_after_the_dev_desk_timeout_of_1m_and_a_takeover_pauses_it_until_handback()
    {
        var ct = TestContext.Current.CancellationToken;
        DevProfile();
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        var took = await worker.ExecuteAsync(() => ThreeCases(sindri, ct), ct);

        // The spec's "under a second" is measured by the Prover on a quiet box. Here the bound only has to catch a test that
        // really waits on the clock (the three cases skip about four minutes), so it cannot flake under a loaded solution run;
        // the measured figure goes to the test output so a regression is visible.
        var ms = took.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        TestContext.Current.SendDiagnosticMessage($"PhaseFiveSignalTests: the three cases took {ms} ms of wall clock");
        Assert.True(took < TimeSpan.FromSeconds(10), $"the three cases took {ms} ms of wall clock; the time-skipping server should run them in well under 10 s");
    }

    /// <summary>The signal's three cases: T-2 parks; T-8 is taken over at 20 s, handed back at 80 s and parks at about 120 s; T-2 resumes.</summary>
    /// <returns>The wall clock the three cases took, from the first start to the last assertion (the worker's start-up excluded).</returns>
    private async Task<TimeSpan> ThreeCases(IServiceProvider sindri, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var env = server.Environment;
        var first = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-2", ct));
        var second = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-8", ct));
        _ = await WorkflowsHarness.Until(first, static v => v.Task is not null);
        _ = await WorkflowsHarness.Until(second, static v => v.Task is not null);

        await env.DelayAsync(TimeSpan.FromSeconds(20), ct);
        var takenOver = await second.ExecuteUpdateAsync(wf => wf.TakeoverAsync(new TakeoverRequested("human:cli", Stamp('1'))), new WorkflowUpdateOptions(new string('1', 32)));
        await env.DelayAsync(TimeSpan.FromSeconds(60), ct);   // T-2 is 80 s in; T-8's paused timer must not fire
        var parked = await WorkflowsHarness.Until(first, static v => v.State == "parked");
        var stillTakenOver = await WorkflowsHarness.Until(second, static v => v.State == "taken-over");
        var refusal = await CompleteWhileTakenOver(second, stillTakenOver);
        var handedBack = await second.ExecuteUpdateAsync(wf => wf.HandbackAsync(new HandbackRequested("human:cli", Stamp('2'))), new WorkflowUpdateOptions(new string('2', 32)));
        await env.DelayAsync(TimeSpan.FromSeconds(30), ct);   // 30 of the 40 s left
        var beforeTheRest = await WorkflowsHarness.Until(second, static v => v.Task is not null);
        await env.DelayAsync(TimeSpan.FromSeconds(15), ct);
        var parkedAfterTheRest = await WorkflowsHarness.Until(second, static v => v.State == "parked");
        var resume = new AnswerGiven("human:cli", Stamp('3'), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
        var resumed = await first.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('3', 32)));

        Assert.Equal((ParkReason.Timeout, "hold ran past 1m", "1.1"), (parked.Question?.Reason, parked.Question?.Question, parked.Run?.Value));
        Assert.Equal(("taken-over", "taken-over"), (takenOver.Instance.State, stillTakenOver.State));
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"hand it back first: sindri workflow handback {stillTakenOver.Address.Value}, then complete run {stillTakenOver.Run!.Value}"), refusal);   // a desk run is reported after the handback
        Assert.InRange(takenOver.Instance.Paused ?? TimeSpan.Zero, TimeSpan.FromSeconds(40) - _slack, TimeSpan.FromSeconds(40) + _slack);
        Assert.Equal(("hold", "1.1"), (handedBack.Instance.State, handedBack.Instance.Run?.Value));
        Assert.Equal(("hold", "1.1", null), (beforeTheRest.State, beforeTheRest.Run?.Value, beforeTheRest.Question));
        Assert.Equal((ParkReason.Timeout, "1.1"), (parkedAfterTheRest.Question?.Reason, parkedAfterTheRest.Run?.Value));
        Assert.Equal(("hold", "1.1", true), (resumed.Instance.State, resumed.Instance.Run?.Value, resumed.Instance.Task is not null));
        var took = clock.Elapsed;
        Assert.Null((await InterpreterEvalTests.Replay(second, ct)).ReplayFailure);
        await first.TerminateAsync("test over");
        await second.TerminateAsync("test over");
        return took;
    }

    /// <summary>A report to the taken-over desk run, which the validator refuses: the fix it words, from after "is parked (Takeover); ".</summary>
    private static async Task<string> CompleteWhileTakenOver(WorkflowHandle<RecipeWorkflow> handle, InstanceView taken)
    {
        var output = new Dictionary<FactKey, FactValue>();
        var report = new StepCompleted("human:cli", taken.Run!.Value, OutcomeName.From("done"), output, Tokens: 0, StepRuntime.Desk, new string('f', 64));
        var refused = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() => handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('4', 32))));
        var message = refused.InnerException!.Message;
        return message[(message.IndexOf("; ", StringComparison.Ordinal) + 2)..];
    }

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));

    /// <summary>recipes/dev/profile.yaml as the home's live profile, so 'LoadRecipe' reads 'workflow.deskTimeout: 1m'.</summary>
    private void DevProfile()
    {
        var profile = Directory.CreateDirectory(Path.Combine(_harness.Home, "profile")).FullName;
        File.WriteAllText(Path.Combine(profile, "profile.yaml"), WorkflowsHarness.Shipped("dev/profile.yaml"));
    }
}
