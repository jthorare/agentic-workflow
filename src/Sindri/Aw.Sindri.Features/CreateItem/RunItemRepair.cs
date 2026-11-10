using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// The repair pass of issue 2588 (<see cref="IRunItemRepair"/>): walks the runs of every workflow, or one run, and has <see cref="RunItemLinker"/> write what a 'create_item' node could not.
/// </summary>
/// <param name="workflows">The workflows, to find their runs.</param>
/// <param name="runs">The runs and their node outputs.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="linker">Repairs one run.</param>
/// <param name="log">Where a skipped run is logged; null logs nowhere.</param>
internal sealed partial class RunItemRepair(IWorkflowStore workflows, IWorkflowRunStore runs, HeldLease held, RunItemLinker linker, ILogger<RunItemRepair>? log = null) : IRunItemRepair
{
    private const int RunsPerWorkflow = 200;

    /// <inheritdoc />
    /// <remarks>It fails only when the workflows or a workflow's runs can't be listed, or the lease is gone. A run that fails is skipped with a warning (its run id, the code, the writes that landed), so one bad run can't keep the pass from finishing.</remarks>
    public async ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken)
    {
        var all = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return all.Error;
        }

        var writes = 0;
        foreach (var workflow in all.Value)
        {
            var recent = await runs.Recent(workflow.Id, 0, RunsPerWorkflow, cancellationToken).ConfigureAwait(false);
            if (recent.IsError)
            {
                return recent.Error;
            }

            foreach (var run in recent.Value)
            {
                if (held.Current is null)
                {
                    return SindriErrors.NotRunning.With("the control plane lost the lease during the repair pass");
                }

                var (landed, failure) = await linker.Repair(run.Id, cancellationToken).ConfigureAwait(false);
                writes += landed;
                if (failure is { } error)
                {
                    Skipped(log ?? NullLogger<RunItemRepair>.Instance, run.Id.Value, error.Code, landed);
                }
            }
        }

        return writes;
    }

    /// <inheritdoc />
    public async ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken)
    {
        var (writes, failure) = await linker.Repair(run, cancellationToken).ConfigureAwait(false);
        return failure is { } error ? error : writes;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The item repair skipped run {Run}: {Code}; {Writes} write(s) landed first, the next pass tries again")]
    private static partial void Skipped(ILogger logger, string run, string code, int writes);
}
