using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The interpreter's activities (engine spec rev 8 §5.1, rev 8.1 §5.3). 'LoadRecipe' (a local activity) reads the
/// approved recipe the workflow input pins, through <see cref="IApprovedRecipes"/>. 'RunStep' is the short activity an
/// 'active' state starts with the run token in its id: in the desk runtime it records the open desk task and returns,
/// and the workflow then waits for the 'complete' Update.
/// </summary>
/// <param name="approvals">The approved recipes.</param>
/// <param name="catalog">The build's block catalog.</param>
/// <param name="times">The profile's 'workflow:' times (S1-D11): 'workflow.deskTimeout' and 'workflow.eventHold'.</param>
/// <param name="blocks">Runs a block-runtime Step (DT-J2).</param>
/// <param name="logger">The log the open desk task is recorded in.</param>
internal sealed partial class RunStepActivities(IApprovedRecipes approvals, BlockCatalog catalog, ProfileWorkflowTimes times, BlockStepRunner blocks, ILogger<RunStepActivities> logger)
{
    /// <summary>Reads the approved recipe and the descriptors of its blocks.</summary>
    /// <param name="recipe">The recipe the instance is pinned to.</param>
    /// <returns>The loaded recipe.</returns>
    [Activity("LoadRecipe")]
    public async Task<LoadedRecipe> LoadRecipeAsync(RecipeVersion recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var found = await approvals.Find(recipe.Id, recipe.Hash, cancel).ConfigureAwait(false);
        if (found.IsError)
        {
            // unknown (SND-RECIPE-002) and not approved or revoked (-003) can't change by retrying; a read that failed (I/O) can
            throw new ApplicationFailureException($"{found.Error.Message}", errorType: found.Error.Code, nonRetryable: string.Equals(found.Error.Code, "SND-RECIPE-002", StringComparison.Ordinal) || string.Equals(found.Error.Code, "SND-RECIPE-003", StringComparison.Ordinal));
        }

        var definition = found.Value.Recipe;
        var used = definition.States.Values.Select(static s => s.Step?.Use)
            .Concat(definition.States.Values.SelectMany(static s => s.Step?.Inputs ?? []).Select(static i => (BlockId?)i.Use))
            .Concat(definition.Transitions.SelectMany(static t => t.Gates.Select(static g => (BlockId?)g.Use).Concat(t.Effects.Select(static e => (BlockId?)e.Use))))
            .OfType<BlockId>().ToHashSet();
        var workflow = await times.Read(cancel).ConfigureAwait(false);
        var delay = await times.DispatchDelay(cancel).ConfigureAwait(false);   // the approved profile's (S1-D11); a read that fails is retried
        return delay.IsError
            ? throw new InvalidOperationException($"{delay.Error.Code}: {delay.Error.Message}")
            : new LoadedRecipe(found.Value.Version, definition, [.. catalog.Blocks.Where(b => used.Contains(b.Id))], workflow.DeskTimeout, workflow.EventHold, delay.Value, blocks.Registered(used));
    }

    /// <summary>
    /// Records the open desk task and returns (rev 8.1 §5.3): the activity, scheduled with the run token in its id
    /// ('run:{token}:{n}'), is the record in the instance's history that a worker restart replays, so the task keeps its
    /// token; it also writes one log line. The workflow then waits for the 'complete' Update. The first-desk-task
    /// notification is P6's ('notify.desk').
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>Done.</returns>
    [Activity("RunStep")]
    public Task RunStepAsync(StepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        DeskTaskOpen(logger, run.Address, run.State, run.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs a block-runtime Step (DT spec §8.2), once: the activity, scheduled with 'block:{token}:{n}' as its id and never retried,
    /// resolves the registered <see cref="IBlockStep"/>, runs it and stores its outputs.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <returns>The outcome and stored outputs, or the failure.</returns>
    [Activity("RunBlockStep")]
    public async Task<BlockStepOutcome> RunBlockStepAsync(BlockStepRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        BlockStepRequested(logger, run.Address, run.State, run.Token);
        var context = ActivityExecutionContext.Current;
        using var done = new CancellationTokenSource();
        var beating = BeatAsync(context, done.Token);
        try
        {
            return await blocks.Run(run, context.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await done.CancelAsync().ConfigureAwait(false);
            await beating.ConfigureAwait(false);
        }
    }

    // Temporal tells a running activity it was cancelled only in the answer to a heartbeat, so a block Step heartbeats while it runs.
    private static async Task BeatAsync(ActivityExecutionContext context, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
            {
                context.Heartbeat();
            }
        }
        catch (OperationCanceledException)
        {
            // boundary: the Step ended, so the beat ends
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "block step requested: {Address} {State} run {Token}")]
    private static partial void BlockStepRequested(ILogger logger, string address, string state, string token);

    [LoggerMessage(Level = LogLevel.Information, Message = "desk task open: {Address} {State} run {Token}")]
    private static partial void DeskTaskOpen(ILogger logger, string address, string state, string token);
}
