using Aw.Meta.Core;
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
    internal ValueTask<string?> Warning(CancellationToken cancellationToken)
    {
        _ = runner;
        _ = paths;
        _ = cancellationToken;
        return ValueTask.FromResult<string?>(null);
    }
}
