using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>The repair after a run's finish is best effort (#2588): an error or an exception is logged and never fails the finish, so a ledger blip cannot retry it.</summary>
public sealed class GraphRunItemRepairTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@1:1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_repair_error_does_not_fail_the_finish()
    {
        var guard = new GraphRunItemRepair(new Repair(SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full"), thrown: null), NullLogger<GraphRunItemRepair>.Instance);

        await guard.AfterFinish(_run, Token);   // does not throw
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repair_that_throws_does_not_fail_the_finish()
    {
        var guard = new GraphRunItemRepair(new Repair(error: null, new InvalidOperationException("boom")), NullLogger<GraphRunItemRepair>.Instance);

        await guard.AfterFinish(_run, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public Task A_host_with_no_repair_does_nothing() => new GraphRunItemRepair(repair: null, NullLogger<GraphRunItemRepair>.Instance).AfterFinish(_run, Token);

    private sealed class Repair(Error? error, Exception? thrown) : IRunItemRepair
    {
        public ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken)
        {
            if (thrown is not null)
            {
                throw thrown;
            }

            Result<int> answer = error is { } failed ? failed : 0;
            return ValueTask.FromResult(answer);
        }
    }
}
