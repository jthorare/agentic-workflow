using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Compares the installed heavy and meta-gate-reserve with the copies in the toolkit checkout (issue #3698). install-cs.sh ships them
/// only on a reinstall, so a box can keep running a wrapper from before a fix.
/// </summary>
/// <param name="runner">Runs cmp.</param>
/// <param name="paths">Names the installed scripts and the checkout.</param>
internal sealed class ScriptStaleCheck(ICommandRunner runner, MetaPaths paths)
{
    /// <summary>The warning line when an installed script differs from the checkout's.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The line, or <see langword="null"/> when every script matches or can't be compared (not installed, no checkout, cmp missing).</returns>
    internal async ValueTask<string?> Warning(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(paths.Toolkit))
        {
            return null;
        }

        var differing = new List<string>();
        foreach (var (installed, script) in new[] { (paths.Heavy, "heavy.sh"), (paths.Reserve, "meta-gate-reserve.sh") })
        {
            var source = Path.Combine(paths.Toolkit, "scripts", script);
            if (!string.IsNullOrEmpty(installed) && await Differs(installed, source, cancellationToken).ConfigureAwait(false))
            {
                differing.Add($"{installed} differs from {source}");
            }
        }

        return differing.Count == 0
            ? null
            : string.Join("; ", differing) + ". Lanes run the installed copy, so a fix in the checkout does not reach them. Fix: scripts/install-cs.sh (publishes and links the build)";
    }

    // cmp exits 0 for the same bytes, 1 for different ones and 2 or more when it can't tell (a missing file, no cmp, a timeout).
    private async ValueTask<bool> Differs(string installed, string source, CancellationToken cancellationToken)
    {
        var run = await runner.Run(new CommandRequest(["cmp", "-s", installed, source], TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
        return run.IsSuccess && !run.Value.TimedOut && run.Value.ExitCode == 1;
    }
}
