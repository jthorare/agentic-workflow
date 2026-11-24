using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>Compares aw-meta, judge and sindri with origin/main's last change to their inputs (issue #4249).</summary>
/// <param name="git">Reads the toolkit checkout.</param>
/// <param name="builds">What install-cs recorded about judge and sindri.</param>
/// <param name="self">This aw-meta's build stamp.</param>
internal sealed class BuildDriftCheck(ToolkitGit git, IInstalledBuilds builds, IBuildInfo self)
{
    /// <summary>The tools this check compares, in order.</summary>
    internal static readonly IReadOnlyList<string> Tools = ["aw-meta", "judge", "sindri"];

    /// <summary>What the comparison is made from, for the cache key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The identity.</returns>
    internal ValueTask<string> Identity(CancellationToken cancellationToken)
    {
        _ = (git, builds, self, cancellationToken);
        throw new NotSupportedException("red: #4249 the identity of the installed builds");
    }

    /// <summary>Compares each tool.</summary>
    /// <param name="toolkit">The toolkit checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One drift per tool, in <see cref="Tools"/> order.</returns>
    internal ValueTask<IReadOnlyList<ToolDrift>> Drifts(string toolkit, CancellationToken cancellationToken)
    {
        _ = (toolkit, cancellationToken);
        throw new NotSupportedException("red: #4249 compares aw-meta, judge and sindri");
    }
}
