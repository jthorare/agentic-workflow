using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.BuildMap;

/// <summary>A record and the files it names, to roll up to the features they are homed in: its node, what its edges cite, and the files.</summary>
/// <param name="Node">The record's node (kept only when a feature takes an edge).</param>
/// <param name="Evidence">The record the edges cite.</param>
/// <param name="Paths">The repo-relative paths it touched, cited or defined, in ordinal order, once each.</param>
internal sealed record RolledRecord(GraphNode Node, RecordKey Evidence, IReadOnlyList<string> Paths)
{
    /// <summary>The tracker project the record (an issue) is in, when the store holds it; its features roll up to the project too.</summary>
    internal NodeKey? Project { get; init; }
}
