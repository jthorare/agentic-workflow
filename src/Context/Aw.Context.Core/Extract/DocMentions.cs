using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>The 'mentions' edges from doc records to the files their text cites, and what didn't resolve (#1373, spec §11.2).</summary>
public static class DocMentions
{
    /// <summary>Resolves what the docs cite against the map's files.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="records">The doc record nodes (<see cref="DocRecords.Find"/>).</param>
    /// <param name="texts">Each doc's scrubbed text by path; null when the scrubber withheld it.</param>
    /// <param name="files">The repo's file paths.</param>
    /// <returns>The edges and the unresolved counts.</returns>
    public static DocMentionScan Find(ContextRepoName repo, IReadOnlyList<GraphNode> records, IReadOnlyDictionary<string, string?> texts, IReadOnlyCollection<string> files)
    {
        _ = (repo, records, texts, files, CitedPaths.Find(string.Empty));
        throw new NotSupportedException();
    }
}
