using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// How the 'complete' Update fails (engine spec rev 8.1 §5.3): a Temporal without serve's namespace is SND-API-001, not
/// a missing instance; with no worker running the Update gives up after its RPC patience (SND-API-002) instead of
/// hanging, and the same report with the same nonce applies once a worker runs.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]   // it asserts the answer arrives within 3 to 10 s of wall clock
public sealed class DeskReportFailureTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Timeout = 120_000)]
    public async Task Complete_on_a_Temporal_without_the_namespace_is_SND_API_001_naming_it()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, static name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? "elsewhere" : null, TestContext.Current.CancellationToken);

        var sent = await served.Sindri.GetRequiredService<TemporalDeskReports>()
            .Complete(InstanceAddress.From("T-41/step/1"), CommandNonce.From(new string('a', 32)), Report(RunToken.For(1, 1), output: null), Token);

        Assert.Equal("SND-API-001", sent.Error.Code);
        Assert.Contains("has no 'elsewhere' namespace", sent.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000)]
    public async Task Complete_with_no_worker_gives_up_cleanly_and_the_same_nonce_applies_once_one_runs()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = await served.Cli("start", "workflow.step", "--item", "T-42", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        var open = await served.Until("T-42/step/1", static v => v.Task is not null);
        var reports = served.Sindri.GetRequiredService<TemporalDeskReports>();
        var output = await served.Sindri.GetRequiredService<IFactStore>().Put("the output"u8.ToArray(), Token);
        var (address, nonce, report) = (InstanceAddress.From("T-42/step/1"), CommandNonce.From(new string('b', 32)), Report(open.Run!.Value, output.Value));
        await served.StopWorker();

        var clock = Stopwatch.StartNew();
        var stranded = await reports.Complete(address, nonce, report, Token);
        var waited = clock.Elapsed;
        await served.RestartWorker();
        var applied = await reports.Complete(address, nonce, report, Token);

        Assert.Equal("SND-API-002", stranded.Error.Code);
        Assert.Contains("--nonce " + new string('b', 32), stranded.Error.Message, StringComparison.Ordinal);
        Assert.InRange(waited, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));   // the 4 s patience is a total (the client core retries a timed-out RPC: 8 s and 12 s were seen); the requirement is to answer inside the CLI's 10 s
        Assert.True(applied.IsSuccess, applied.IsError ? applied.Error.Message : null);
        Assert.NotEqual(open.Run, (await served.Until("T-42/step/1", v => v.Run != open.Run)).Run);
    }

    private static StepCompleted Report(RunToken run, FactValue? output) =>
        new("human:test", run, OutcomeName.From("done"),
            output is { } o ? new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = o } : [],
            Tokens: 0, StepRuntime.Desk, new string('c', 64));
}
