using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The one catch ladder every Update on an instance's workflow shares (review run 4, arch-r4-3; P7 D11).</summary>
[Lane(TestLane.Unit)]
public sealed class TemporalUpdatesLadderTests
{
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('d', 32));
    private static readonly InstanceAddress _address = InstanceAddress.From("T-1/step/1");

    [Fact(Timeout = 30_000)]
    public async Task One_ladder_maps_refusal_namespace_timeout_notfound_unavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var refused = await Run(static _ => throw new WorkflowUpdateFailedException(new ApplicationFailureException("not here", errorType: "SND-WF-005")), ct);
        var unknownRefusal = await Run(static _ => throw new WorkflowUpdateFailedException(new ApplicationFailureException("odd", errorType: "SND-ZZZ-999")), ct);
        var noNamespace = await Run(static _ => throw new RpcException(RpcException.StatusCode.NotFound, "namespace default not found", rawStatus: null), ct);
        var missing = await Run(static _ => throw new RpcException(RpcException.StatusCode.NotFound, "workflow execution not found", rawStatus: null), ct);
        var timedOut = await Run(static _ => throw new RpcException(RpcException.StatusCode.DeadlineExceeded, "deadline", rawStatus: null), ct);
        var down = await Run(static _ => throw new RpcException(RpcException.StatusCode.Unavailable, "connection refused", rawStatus: null), ct);

        Assert.Equal(
            ["SND-WF-005", "SND-WF-003", "SND-API-001", "SND-WF-001", "SND-API-002", "SND-API-001"],
            [Code(refused), Code(unknownRefusal), Code(noNamespace), Code(missing), Code(timedOut), Code(down)]);
        Assert.Contains($"--nonce {_nonce.Value}", down.Error.Message, StringComparison.Ordinal);
        Assert.Contains("namespace default not found", noNamespace.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_timeout_against_a_frontend_that_fails_its_health_check_is_down()
    {
        var frontend = new TemporalUpdates.UpdateFrontend("127.0.0.1:7233", "default", static _ => Task.FromResult(false));

        var result = await TemporalUpdates.Execute(frontend, _address, _nonce.Value, static _ => throw new RpcException(RpcException.StatusCode.DeadlineExceeded, "deadline", rawStatus: null), TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-001", Code(result));
    }

    [Fact(Timeout = 30_000)]
    public async Task The_updates_outcome_is_returned_unchanged()
    {
        var outcome = new CommandResult(new InstanceView(_address, new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "parked", Detail: null, DateTimeOffset.UnixEpoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null) { Id = WorkflowInstanceId.Of(_address) }, "resumed");
        var result = await Run(_ => Task.FromResult(outcome), TestContext.Current.CancellationToken);

        Assert.Same(outcome, result.Value);
    }

    private static ValueTask<Kernel.Result<CommandResult>> Run(Func<RpcOptions, Task<CommandResult>> send, CancellationToken ct) =>
        TemporalUpdates.Execute(new TemporalUpdates.UpdateFrontend("127.0.0.1:7233", "default", static _ => Task.FromResult(true)), _address, _nonce.Value, send, ct);

    private static string Code(Kernel.Result<CommandResult> result) => result.IsError ? result.Error.Code : "accepted";
}
