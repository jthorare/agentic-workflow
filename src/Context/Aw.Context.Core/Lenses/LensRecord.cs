using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>A record (a ticket, a doc, a thread) that mentions scope nodes. Its text is never fetched here.</summary>
/// <param name="Key">The record's node key.</param>
/// <param name="Label">Its label as the caller reads it.</param>
/// <param name="Source">Where it lives: 'linear', 'linear:doc', 'slack', 'onyx' or 'repo-doc'.</param>
/// <param name="Mentions">The scope nodes it mentions.</param>
/// <param name="Trust">Record text is untrusted.</param>
/// <param name="State">The citation's state; 'unchecked' until a source is read.</param>
/// <param name="Cites">The citation markers (empty in S-4).</param>
/// <param name="Kind">Its kind (always a record).</param>
public sealed record LensRecord(NodeKey Key, string Label, string Source, IReadOnlyList<NodeKey> Mentions, Results.Trust Trust, CitationState State, IReadOnlyList<string> Cites, NodeKind Kind)
{
    /// <summary>Schema 2 only: every anchor that reached it, in request order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Anchors { get; init; }

    /// <summary>When the record was last written at its source (a ticket's update time, a doc's head commit time); left out when the node has none.</summary>
    [JsonPropertyName("at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? At { get; init; }

    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }

    /// <summary>Schema 2 only: the fewest edges or cascade levels from any anchor that reached it (0 for an anchor's own scope).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Depth { get; init; }
}
