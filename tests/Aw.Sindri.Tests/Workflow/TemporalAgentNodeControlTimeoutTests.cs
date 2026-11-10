using System.Diagnostics;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The run view asks the 'agent-node' query on every read, so a server that never answers must give SND-API-001 within the limit and never hang the read (#2472).</summary>
[Lane(TestLane.Unit)]
public sealed class TemporalAgentNodeControlTimeoutTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(WorkflowSlug.From("morning-check"), 1, 1);

    [Fact]
    public async Task A_server_that_never_answers_gives_engine_unavailable_within_the_limit()
    {
        var control = new TemporalAgentNodeControl(Hang, queryLimit: TimeSpan.FromMilliseconds(150));
        var clock = Stopwatch.StartNew();

        var running = await control.Running(_run, TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, running.Error.Code);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"the query took {clock.Elapsed}");
    }

    [Fact]
    public async Task A_stop_the_caller_asked_for_is_not_turned_into_an_error()
    {
        var control = new TemporalAgentNodeControl(Hang, queryLimit: TimeSpan.FromMinutes(5));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await control.Running(_run, caller.Token));
    }

    private static async ValueTask<Result<ITemporalClient>> Hang(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return SindriErrors.EngineUnavailable.With("unreachable: the delay only ends by cancellation");
    }
}
