using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Runs one of the macOS tools (sysctl, vm_stat, ps) through the command runner and returns all of its output (ps lists far more than the runner's default tail), or AW-META-010 naming the tool and why.</summary>
internal static class MacTools
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    internal static async ValueTask<Result<string>> Output(ICommandRunner runner, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        var ran = await runner.Run(new CommandRequest(argv, _timeout) { TailCharacters = int.MaxValue }, cancellationToken).ConfigureAwait(false);
        var tool = string.Join(' ', argv);
        if (ran.IsError)
        {
            return MetaErrors.HostUnreadable.With($"unavailable on macOS: {tool} could not be run ({ran.Error.Message})");
        }

        return ran.Value.TimedOut || ran.Value.ExitCode != 0
            ? MetaErrors.HostUnreadable.With($"unavailable on macOS: {tool} {(ran.Value.TimedOut ? "timed out" : string.Create(CultureInfo.InvariantCulture, $"exited {ran.Value.ExitCode}"))}: {ran.Value.Stderr.Trim()}")
            : ran.Value.Stdout;
    }
}
