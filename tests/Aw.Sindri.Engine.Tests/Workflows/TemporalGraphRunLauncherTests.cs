using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// What the launcher says when Temporal's answer to a start is lost (issue 2384): only 'nothing ran' may be reported as an unreachable engine,
/// because the starter discards or requeues the row on it and the next start of the same run id would then fail with SND-GRAPH-100 while the walk keeps running.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalGraphRunLauncherTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static int _count;

    private enum Loss
    {
        /// <summary>The start reaches Temporal and is accepted; the reply never arrives.</summary>
        ReplyLostAfterAccept,

        /// <summary>The call fails before Temporal started anything.</summary>
        FailsBeforeStart,
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_timeout_after_Temporal_accepted_the_start_is_launched()
    {
        var launcher = LauncherThatLoses(RpcException.StatusCode.DeadlineExceeded, Loss.ReplyLostAfterAccept);

        var launched = await launcher.Launch(NewRun(), Token);

        Assert.True(launched.IsSuccess, launched.IsError ? launched.Error.Code : null);
    }

    [Fact]
    public async Task An_unavailable_server_is_nothing_ran()
    {
        var launcher = LauncherThatLoses(RpcException.StatusCode.Unavailable, Loss.FailsBeforeStart);

        var launched = await launcher.Launch(NewRun(), Token);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, launched.Error.Code);
    }

    [Fact]
    public async Task A_timeout_with_no_workflow_behind_it_is_nothing_ran()
    {
        var launcher = LauncherThatLoses(RpcException.StatusCode.DeadlineExceeded, Loss.FailsBeforeStart);

        var launched = await launcher.Launch(NewRun(), Token);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, launched.Error.Code);
    }

    [Fact]
    public async Task A_start_of_a_run_id_Temporal_already_has_is_launched()
    {
        var run = NewRun();
        var launcher = new TemporalGraphRunLauncher(_ => ValueTask.FromResult(Results.Success<ITemporalClient>(server.Environment.Client)), TaskQueue());
        _ = (await launcher.Launch(run, Token)).Value;

        var again = await launcher.Launch(run, Token);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.Code : null);
    }

    private static string Next() => Interlocked.Increment(ref _count).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string TaskQueue() => $"launcher-test-{Next()}";

    private static WorkflowRun NewRun() =>
        new(WorkflowRunId.Of(WorkflowSlug.From($"launcher-test-{Next()}"), revision: 1, occurrence: 1), _workflow, Revision: 1, """{"kind":"manual"}""",
            WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);

    private TemporalGraphRunLauncher LauncherThatLoses(RpcException.StatusCode code, Loss loss)
    {
        var options = (TemporalClientOptions)server.Environment.Client.Options.Clone();
        options.Interceptors = [.. options.Interceptors ?? [], new LosingReply(code, loss)];
        ITemporalClient client = new TemporalClient(server.Environment.Client.Connection, options);
        return new TemporalGraphRunLauncher(_ => ValueTask.FromResult(Results.Success(client)), TaskQueue());
    }

    private sealed class LosingReply(RpcException.StatusCode code, Loss loss) : IClientInterceptor
    {
        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) => new Outbound(nextInterceptor, code, loss);

        private sealed class Outbound(ClientOutboundInterceptor next, RpcException.StatusCode code, Loss loss) : ClientOutboundInterceptor(next)
        {
            public override async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(StartWorkflowInput input)
            {
                if (loss == Loss.ReplyLostAfterAccept)
                {
                    _ = await base.StartWorkflowAsync<TWorkflow, TResult>(input).ConfigureAwait(false);
                }

                throw new RpcException(code, "the reply was lost", rawStatus: null);
            }
        }
    }
}
