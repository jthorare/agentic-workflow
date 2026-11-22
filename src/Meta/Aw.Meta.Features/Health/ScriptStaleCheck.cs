using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Compares the installed heavy and meta-gate-reserve with origin/main's copies in the toolkit checkout (issue #3698). install-cs.sh ships
/// them only on a reinstall, so a box can keep running a wrapper from before a fix. It compares with origin/main, not the checkout's files:
/// the toolkit link is often a parked checkout on an old commit, and an install from a newer tree would warn on every run.
/// </summary>
/// <param name="runner">Runs git.</param>
/// <param name="paths">Names the installed scripts and the checkout.</param>
internal sealed class ScriptStaleCheck(ICommandRunner runner, MetaPaths paths)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>The warning line when an installed script differs from origin/main's.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The line, or <see langword="null"/> when every script matches or can't be compared (not installed, no checkout, no origin/main, git failed or timed out).</returns>
    internal async ValueTask<string?> Warning(CancellationToken cancellationToken)
    {
        if (paths.Toolkit is not { Length: > 0 } toolkit)
        {
            return null;
        }

        var differing = new List<string>();
        foreach (var (installed, script) in new[] { (paths.Heavy, "heavy.sh"), (paths.Reserve, "meta-gate-reserve.sh") })
        {
            if (!string.IsNullOrEmpty(installed) && await Differs(toolkit, installed, script, cancellationToken).ConfigureAwait(false))
            {
                differing.Add($"{installed} differs from origin/main's scripts/{script}");
            }
        }

        return differing.Count == 0 ? null : string.Join("; ", differing) + ". Fix: scripts/install-cs.sh (publishes and links the build)";
    }

    private async ValueTask<bool> Differs(string toolkit, string installed, string script, CancellationToken cancellationToken)
    {
        var mine = await Hash(["git", "-C", toolkit, "hash-object", installed], cancellationToken).ConfigureAwait(false);
        var theirs = mine is null ? null : await Hash(["git", "-C", toolkit, "rev-parse", $"origin/main:scripts/{script}"], cancellationToken).ConfigureAwait(false);
        return mine is not null && theirs is not null && !string.Equals(mine, theirs, StringComparison.Ordinal);
    }

    // The first line of stdout when git exits 0 in time; null for anything else (a missing file, no origin/main, a timeout).
    private async ValueTask<string?> Hash(string[] argv, CancellationToken cancellationToken)
    {
        var run = await runner.Run(new CommandRequest(argv, _timeout), cancellationToken).ConfigureAwait(false);
        if (!run.IsSuccess || run.Value.TimedOut || run.Value.ExitCode != 0)
        {
            return null;
        }

        var text = run.Value.Stdout.Trim();
        return text.Length == 0 ? null : text;
    }
}
