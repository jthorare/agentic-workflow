using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Every installed tool compared with what origin/main would install, for health (issues #3698, #4249): heavy and meta-gate-reserve
/// by content, aw-meta, judge and sindri by the commit they were built from. Nothing is rebuilt or reinstalled.
/// </summary>
/// <param name="scripts">Compares heavy and meta-gate-reserve.</param>
/// <param name="builds">Compares aw-meta, judge and sindri.</param>
/// <param name="git">Reads origin/main.</param>
/// <param name="values">Keeps the last comparison of the builds.</param>
/// <param name="paths">Names the toolkit checkout.</param>
internal sealed class ToolDriftCheck(ScriptStaleCheck scripts, BuildDriftCheck builds, ToolkitGit git, IKeyValueStore values, MetaPaths paths)
{
    /// <summary>Compares every installed tool.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One drift per tool; null when there is no toolkit checkout to compare with.</returns>
    internal ValueTask<IReadOnlyList<ToolDrift>?> Drifts(CancellationToken cancellationToken)
    {
        _ = (scripts, builds, git, values, paths, cancellationToken);
        throw new NotSupportedException("red: #4249 compares every installed tool");
    }
}
