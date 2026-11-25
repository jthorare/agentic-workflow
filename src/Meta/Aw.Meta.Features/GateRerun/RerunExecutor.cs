using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.GateRerun;

/// <summary>Runs one test of a step in a tree and reads how it went. With filterMustMatch, a run that matched no test is an error.</summary>
/// <param name="runner">Runs the command.</param>
/// <param name="paths">Names the heavy-job wrapper.</param>
internal sealed class RerunExecutor(ICommandRunner runner, MetaPaths paths)
{
    private const int NoTestsRan = 8;

    // The shell's "command not found" or "No such file or directory": the rerun never reached a test, whatever the command.
    private const int CommandNotFound = 127;
    private static readonly TimeSpan _limit = TimeSpan.FromMinutes(45);

    /// <summary>The command that runs one test of the step, or null when the step can't.</summary>
    internal IReadOnlyList<string>? Command(StepConfig step, string test) => GateCommand.Rerun(step, test, paths.Heavy);

    internal async ValueTask<Result<RerunOutcome>> Run(RerunJob job, CancellationToken cancellationToken)
    {
        var (argv, tree, test, filterMustMatch, log) = job;
        var request = Request(argv, tree, log);
        var ran = await runner.Run(request, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var result = ran.Value;
        var output = result.Stdout + "\n" + result.Stderr;
        if (RanNothing(result, output, argv) is { } nothing)
        {
            // On the gate tree the test exists, so a filter that matches nothing is a bad filter. On main it is a test the PR adds.
            var reason = nothing == CommandNotFound ? $"the rerun command, or something it ran, was not found for '{test}' (exit {CommandNotFound})" : $"no test matched '{test}'";
            return filterMustMatch ? MetaErrors.Invalid.With(reason) : new RerunOutcome(Passed: false, nothing == CommandNotFound ? "command not found" : "no test matched", Absent: true);
        }

        // Exit 8 with tests run is some project having none to run, as in a solution-wide filter: the tests that ran passed.
        return result is { ExitCode: 0 or NoTestsRan, TimedOut: false }
            ? new RerunOutcome(Passed: true)
            : new RerunOutcome(Passed: false, result.TimedOut ? "timed out" : FailureExcerpt.For(output, test));
    }

    // The exit code that says the rerun ran nothing, or null. 127 counts for every command; "no test matched" only for dotnet.
    private static int? RanNothing(CommandResult result, string output, IReadOnlyList<string> argv)
    {
        if (result.TimedOut)
        {
            return null;
        }

        if (result.ExitCode == CommandNotFound)
        {
            return CommandNotFound;
        }

        var none = TestNames.RanNone(output) || (result.ExitCode == NoTestsRan && !TestNames.Ran(output));
        return none && argv.Contains("dotnet", StringComparer.Ordinal) ? NoTestsRan : null;
    }

    // A dotnet rerun needs the real home (NuGet, the SDK's state); any other command is a suite, which runs as bash-tests.sh runs it.
    // The heavy wrapper keeps the real state dir (its locks) and DOTNET_ROOT: only the command behind it is isolated.
    private CommandRequest Request(IReadOnlyList<string> argv, string tree, string log) => new(argv, _limit)
    {
        Cwd = tree,
        PathPrefix = GateTools.PathPrefix(tree),
        Env = GatePriority.Environment,
        UnsetEnv = GatePriority.Scrubbed,
        Isolated = !argv.Contains("dotnet", StringComparer.Ordinal),
        IsolatedFrom = paths.Heavy is { } heavy && argv.Count > 0 && argv[0] == heavy ? 1 : 0,
        LogFile = log,
    };

    /// <summary>The same command without --no-build, for a tree nobody built.</summary>
    internal static IReadOnlyList<string> Building(IReadOnlyList<string> argv) => [.. argv.Where(word => word != "--no-build")];
}
