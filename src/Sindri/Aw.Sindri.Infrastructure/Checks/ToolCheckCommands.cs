using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Checks;

/// <summary><see cref="ICheckCommands"/> over <see cref="IToolRunner"/>: the command runs in the worktree, and what it printed is scrubbed and cut to its tail before anyone sees it.</summary>
/// <param name="runner">Runs the program.</param>
/// <param name="scrubber">Takes secrets out of what it printed.</param>
/// <param name="clock">Times the run.</param>
internal sealed class ToolCheckCommands(IToolRunner runner, ITextScrubber scrubber, TimeProvider clock) : ICheckCommands
{
    public async ValueTask<Result<CheckRun>> Run(CheckInvocation command, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Argv.Count == 0 || string.IsNullOrWhiteSpace(command.Argv[0]))
        {
            return SindriErrors.InvalidInput.With($"the command '{command.Name}' names no program to run");
        }

        // why: the Step holds the box's heavy slot for the whole batch; a command that runs the repo's own heavy wrapper must not queue behind the slot its caller holds
        var options = new ToolRunOptions(Environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_HEAVY_LOCK_HELD"] = "1" }, WorkingDirectory: workingDirectory);
        var started = clock.GetTimestamp();
        var run = await Task.Run(() => runner.Run(command.Argv[0], [.. command.Argv.Skip(1)], command.Timeout, options, cancellationToken), CancellationToken.None).ConfigureAwait(false);   // the runner blocks until the tool ends; it kills the tool on cancel
        var took = clock.GetElapsedTime(started);
        return run.Outcome switch
        {
            ToolOutcome.Cancelled => throw new OperationCanceledException(cancellationToken),
            ToolOutcome.Missing => SindriErrors.InvalidInput.With($"'{command.Argv[0]}' (the command '{command.Name}') can't be started: no such program, or no such directory {workingDirectory}"),
            ToolOutcome.TimedOut => CheckRun.Of(-1, timedOut: true, took, scrubber.Scrub(run.Output + run.Error)),
            ToolOutcome.Exited => CheckRun.Of(run.ExitCode, timedOut: false, took, scrubber.Scrub(run.Output + run.Error)),
            _ => throw new InvalidOperationException("unknown tool outcome"),
        };
    }
}
