using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Compares the installed heavy and meta-gate-reserve with origin/main's copies in the toolkit checkout (issue #3698). install-cs.sh ships
/// them only on a reinstall, so a box can keep running a wrapper from before a fix. It compares with origin/main, not the checkout's files:
/// the toolkit link is often a parked checkout on an old commit, and an install from a newer tree would warn on every run. A script that
/// can't be compared (not installed, no origin/main, git failed or timed out) is unknown, never current (issue #4249).
/// </summary>
/// <param name="runner">Runs git.</param>
/// <param name="paths">Names the installed scripts and the checkout.</param>
internal sealed class ScriptStaleCheck(ICommandRunner runner, MetaPaths paths)
{
    private const string Fix = "scripts/install-cs.sh --publish --link";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <summary>Compares each installed script.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One drift per script the paths name; none without a toolkit checkout.</returns>
    internal async ValueTask<IReadOnlyList<ToolDrift>> Drifts(CancellationToken cancellationToken)
    {
        if (paths.Toolkit is not { Length: > 0 } toolkit)
        {
            return [];
        }

        var drifts = new List<ToolDrift>();
        foreach (var (tool, installed, script) in new[] { ("heavy", paths.Heavy, "heavy.sh"), ("meta-gate-reserve", paths.Reserve, "meta-gate-reserve.sh") })
        {
            if (!string.IsNullOrEmpty(installed))
            {
                drifts.Add(await Compare(toolkit, tool, installed, script, cancellationToken).ConfigureAwait(false));
            }
        }

        return drifts;
    }

    private async ValueTask<ToolDrift> Compare(string toolkit, string tool, string installed, string script, CancellationToken cancellationToken)
    {
        var mine = await Hash(["git", "-C", toolkit, "hash-object", installed], cancellationToken).ConfigureAwait(false);
        if (mine is null)
        {
            return new ToolDrift(tool, DriftState.Unknown, $"git could not read {installed} (not installed?); fix: {Fix}");
        }

        var theirs = await Hash(["git", "-C", toolkit, "rev-parse", $"origin/main:scripts/{script}"], cancellationToken).ConfigureAwait(false);
        if (theirs is null)
        {
            return new ToolDrift(tool, DriftState.Unknown, $"no origin/main:scripts/{script} in {toolkit} to compare with; fix: git -C {toolkit} fetch origin");
        }

        return string.Equals(mine, theirs, StringComparison.Ordinal)
            ? new ToolDrift(tool, DriftState.Current, $"{installed} matches origin/main's scripts/{script}")
            : new ToolDrift(tool, DriftState.Stale, $"{installed} differs from origin/main's scripts/{script}; fix: {Fix}");
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
