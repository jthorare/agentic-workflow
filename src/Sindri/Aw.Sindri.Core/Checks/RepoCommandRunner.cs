using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// Runs the repo's commands (<c language="text">workflow.build.commands</c>, line spec LN-WF9) for a build or ship Step: reads them from its source and runs a batch in the phase worktree
/// while holding one heavy slot, released on every path.
/// </summary>
/// <param name="source">The repo's commands.</param>
/// <param name="commands">Runs one command.</param>
/// <param name="slots">The box-wide heavy-job lock.</param>
/// <param name="clock">Times the budget.</param>
public sealed class RepoCommandRunner(IBuildCommandSource source, ICheckCommands commands, IHeavySlots slots, TimeProvider clock)
{
    private static readonly TimeSpan _reportMargin = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan _slotWait = TimeSpan.FromMinutes(30);

    /// <summary>The budget a Step gives its commands: its MaxDuration less a margin to write its report.</summary>
    /// <param name="maxDuration">The Step's MaxDuration.</param>
    /// <returns>The time the slot wait and every command share.</returns>
    public static TimeSpan BudgetFor(TimeSpan maxDuration) => maxDuration > _reportMargin ? maxDuration - _reportMargin : TimeSpan.Zero;

    /// <summary>The commands the approved profile lists, in the profile's order.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The commands; none when no profile is approved or it lists none.</returns>
    public ValueTask<Result<CommandSet>> For(CancellationToken cancellationToken) => source.Read(cancellationToken);

    /// <summary>Runs every command in order, in <paramref name="workingDirectory"/>, under one heavy slot. A failing command does not stop the rest.</summary>
    /// <param name="holder">Who holds the slot, for the lock's owner file ('sindri step.build.checks').</param>
    /// <param name="set">The commands, and the longest each may run.</param>
    /// <param name="workingDirectory">The phase worktree.</param>
    /// <param name="budget">The time the slot wait and every command share (<see cref="BudgetFor"/>); a command gets what is left of it.</param>
    /// <param name="cancellationToken">Cancels the batch: the running command is killed and the call throws.</param>
    /// <returns>How each ended; an error when the slot stayed busy, the budget ran out or a command could not be started.</returns>
    public ValueTask<Result<IReadOnlyList<CheckResult>>> Run(
        string holder, CommandSet set, string workingDirectory, TimeSpan budget, CancellationToken cancellationToken) =>
        Execute(holder, new Batch(set, workingDirectory, StopAtFailure: false), budget, cancellationToken);

    /// <summary>Runs the commands in order like <see cref="Run"/> but stops after the first one that does not pass, for a Step that needs only one failure.</summary>
    /// <param name="holder">Who holds the slot, for the lock's owner file.</param>
    /// <param name="set">The commands, and the longest each may run.</param>
    /// <param name="workingDirectory">The phase worktree.</param>
    /// <param name="budget">The time the slot wait and every command share.</param>
    /// <param name="cancellationToken">Cancels the batch: the running command is killed and the call throws.</param>
    /// <returns>How each command that ran ended, the last being the failing one; an error as for <see cref="Run"/>.</returns>
    public ValueTask<Result<IReadOnlyList<CheckResult>>> RunUntilFailure(
        string holder, CommandSet set, string workingDirectory, TimeSpan budget, CancellationToken cancellationToken) =>
        Execute(holder, new Batch(set, workingDirectory, StopAtFailure: true), budget, cancellationToken);

    private async ValueTask<Result<IReadOnlyList<CheckResult>>> Execute(string holder, Batch batch, TimeSpan budget, CancellationToken cancellationToken)
    {
        var set = batch.Set;
        ArgumentNullException.ThrowIfNull(set);
        if (set.Commands.Count == 0)
        {
            return Results.Success<IReadOnlyList<CheckResult>>([]);
        }

        var deadline = clock.GetUtcNow() + budget;

        // Until the engine grants a recipe-level slot wait (LN-WF3, `waitFor: { resource: heavy }`), the Step takes its own slot here. When LN-WF3 lands this must go: the instance
        // would hold two slots, and the box's cap counts both.
        var wait = budget < _slotWait ? budget : _slotWait;
        var taken = await slots.Take(holder, wait, waiting: null, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return taken.Error;
        }

        if (taken.Value.Lease is not { } lease)
        {
            return WorkflowErrors.EffectFailed.With($"every heavy slot stayed busy for {Minutes(wait)} min: {taken.Value.Busy}");
        }

        await using (lease.ConfigureAwait(false))
        {
            return await RunAll(batch, new Allowance(deadline, budget), cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Minutes(TimeSpan time) => time.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);

    private static Error OutOfBudget(Allowance allowance, string what) =>
        WorkflowErrors.EffectFailed.With($"the Step's budget of {Minutes(allowance.Budget)} min ran out {what} (fix: make the commands faster, or list fewer of them in workflow.build.commands)");

    private async ValueTask<Result<IReadOnlyList<CheckResult>>> RunAll(Batch batch, Allowance allowance, CancellationToken cancellationToken)
    {
        var (set, workingDirectory, stopAtFailure) = batch;
        var results = new List<CheckResult>(set.Commands.Count);
        foreach (var command in set.Commands)
        {
            var left = allowance.Deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return OutOfBudget(allowance, $"before '{command.Name}' could start ({results.Count} of {set.Commands.Count} commands ran)");
            }

            var own = set.Each < left;
            var invocation = new CheckInvocation(command.Name, command.Run, own ? set.Each : left);
            var run = await commands.Run(invocation, workingDirectory, cancellationToken).ConfigureAwait(false);
            if (run.IsError)
            {
                return run.Error;
            }

            if (!own && run.Value.TimedOut)
            {
                return OutOfBudget(allowance, $"while '{command.Name}' was running ({results.Count} of {set.Commands.Count} commands ran before it){TailOf(run.Value)}");
            }

            results.Add(new CheckResult(command, run.Value));
            if (stopAtFailure && !run.Value.Passed)
            {
                break;
            }
        }

        return results;
    }

    private static string TailOf(CheckRun run) => string.IsNullOrWhiteSpace(run.Tail) ? string.Empty : $"; it had printed: {run.Tail}";

    private sealed record Batch(CommandSet Set, string WorkingDirectory, bool StopAtFailure);

    private sealed record Allowance(DateTimeOffset Deadline, TimeSpan Budget);
}
