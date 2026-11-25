using Aw.Kernel;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Runs one gate step in the gate tree, its output going to the step's log.</summary>
/// <param name="runner">Runs the step's command.</param>
/// <param name="paths">Names the heavy-job wrapper.</param>
public sealed class GateStepRunner(ICommandRunner runner, MetaPaths paths)
{
    private static readonly TimeSpan _limit = TimeSpan.FromHours(2);

    /// <summary>Runs the step.</summary>
    /// <param name="step">The step.</param>
    /// <param name="tree">The gate tree to run in.</param>
    /// <param name="log">The log file.</param>
    /// <param name="heavySlots">The gate's heavy-slot limit, given to the heavy-job wrapper as its per-session slot count.</param>
    /// <param name="cancellationToken">Cancels the step (its process tree is stopped).</param>
    /// <returns>The step with its verdict, or the error that kept it from starting or from getting a heavy slot (the wrapper's exit 75).</returns>
    public async ValueTask<Result<GateStep>> Run(StepConfig step, string tree, string log, int heavySlots, CancellationToken cancellationToken)
    {
        var argv = GateCommand.For(step, paths.Heavy);
        var request = new CommandRequest(argv, _limit) { Cwd = tree, PathPrefix = GateTools.PathPrefix(tree), LogFile = log, Env = GatePriority.EnvironmentFor(heavySlots), UnsetEnv = GatePriority.Scrubbed, TermGrace = step.TermGraceSeconds is > 0 and var seconds ? TimeSpan.FromSeconds(seconds) : null };
        var ran = await runner.Run(request, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        // The wrapper giving up on a lock says nothing about the step: an infrastructure error, so it never counts as a red gate.
        if (ran.Value is { ExitCode: GatePriority.LockTimeout, TimedOut: false } && paths.Heavy is not null && argv[0] == paths.Heavy)
        {
            return MetaErrors.CommandNotStarted.With($"{step.Name}: the heavy-job wrapper gave up waiting for a lock (exit {GatePriority.LockTimeout})");
        }

        return new GateStep(step.Name, ran.Value is { ExitCode: 0, TimedOut: false } ? GateVerdict.Pass : GateVerdict.Fail, log);
    }
}
