using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Runs one of the macOS tools (sysctl, vm_stat, ps) through the command runner and returns all of its output (ps lists far more than the runner's default tail), or AW-META-010 naming the tool and why.</summary>
internal static class MacTools
{
    /// <summary>
    /// How many characters of a tool's output are kept: 16 MiB, about thirty times what "ps -axww" prints for a busy machine
    /// (a few thousand processes with long command lines, well under 1 MiB). Output that reaches it is refused, because the runner keeps only the end and the start would be missing.
    /// </summary>
    internal const int OutputCharacterCap = 16 * 1024 * 1024;

    /// <summary>How many characters of a tool's stderr go into an error message.</summary>
    internal const int StderrCharacterCap = 300;

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    /// <summary>Runs a tool and returns all of its output.</summary>
    /// <param name="runner">The command runner.</param>
    /// <param name="argv">The tool and its arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="exit1WithOutputIsUsable">True for a tool (lsof) that exits 1 when it could not read some of what it lists: exit 1 with output is then an answer.</param>
    /// <returns>The output, or AW-META-010 (also when the output reached the cap: the start would be missing).</returns>
    internal static async ValueTask<Result<string>> Output(ICommandRunner runner, IReadOnlyList<string> argv, CancellationToken cancellationToken, bool exit1WithOutputIsUsable = false)
    {
        var ran = await runner.Run(new CommandRequest(argv, _timeout) { TailCharacters = OutputCharacterCap }, cancellationToken).ConfigureAwait(false);
        var tool = string.Join(' ', argv);
        if (ran.IsError)
        {
            return MetaErrors.HostUnreadable.With($"unavailable on macOS: {tool} could not be run ({ran.Error.Message})");
        }

        var usableExit = ran.Value.ExitCode == 0 || (exit1WithOutputIsUsable && ran.Value.ExitCode == 1 && ran.Value.Stdout.Length > 0);
        if (ran.Value.TimedOut || !usableExit)
        {
            return MetaErrors.HostUnreadable.With($"unavailable on macOS: {tool} {(ran.Value.TimedOut ? "timed out" : string.Create(CultureInfo.InvariantCulture, $"exited {ran.Value.ExitCode}"))}: {Clip(ran.Value.Stderr.Trim())}");
        }

        return ran.Value.Stdout.Length >= OutputCharacterCap
            ? MetaErrors.HostUnreadable.With($"unavailable on macOS: {Path.GetFileName(argv[0])} output over the cap")
            : ran.Value.Stdout;
    }

    /// <summary>Cuts a tool's stderr to <see cref="StderrCharacterCap"/> characters, marking the cut.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or its start and a marker.</returns>
    internal static string Clip(string text) => text.Length <= StderrCharacterCap ? text : text[..StderrCharacterCap] + "... (truncated)";
}
