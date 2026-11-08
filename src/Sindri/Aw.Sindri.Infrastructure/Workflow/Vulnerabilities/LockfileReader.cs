#pragma warning disable MA0182 // why: red stub; the green commit registers it
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

/// <summary>Reads packages.lock.json, package-lock.json, go.sum, Cargo.lock and poetry.lock under a directory.</summary>
internal static class LockfileReader
{
    /// <summary>Reads every supported lockfile in <paramref name="directory"/> (the repo root and its subfolders, not node_modules or .git).</summary>
    /// <param name="directory">The repo's checkout.</param>
    /// <returns>The packages, once each; or why the directory could not be read.</returns>
    public static Result<IReadOnlyList<VulnPackage>> ReadDirectory(string directory) => throw new NotSupportedException(directory);
}
