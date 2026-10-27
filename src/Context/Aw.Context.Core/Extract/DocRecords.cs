using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>The doc records of a map build (#1373, spec §11.2): one 'record' node for every doc of the repo, linked by a rule or not.</summary>
public static class DocRecords
{
    /// <summary>Finds the repo's docs among its paths.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="paths">Every path of the repo at the commit, '/'-separated.</param>
    /// <param name="docGlobs">'context.docs.globs'.</param>
    /// <param name="directoryNames">'context.docs.directoryNames': file names that match in any directory.</param>
    /// <param name="at">The head commit's time.</param>
    /// <returns>The nodes, in path order.</returns>
    public static IReadOnlyList<GraphNode> Find(ContextRepoName repo, IReadOnlyList<string> paths, IReadOnlyList<string> docGlobs, IReadOnlyList<string> directoryNames, DateTimeOffset at) =>
        throw new NotSupportedException();
}
