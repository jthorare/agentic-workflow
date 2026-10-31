using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.WholeMap;

/// <summary>One node of the whole map: no evidence, quote or decision text (the inspector reads those per node).</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Label">The label as the caller reads it.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Area">The area it belongs to.</param>
/// <param name="AlsoIn">Up to 5 other areas it reaches, in order of how many edges reach them.</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Confirmed">True when a human or the extractor stands behind it.</param>
public sealed record WholeMapNode(NodeKey Key, string Label, NodeKind Kind, string Area, IReadOnlyList<string> AlsoIn, Results.Trust Trust, Provenance Provenance, double Confidence, bool Confirmed)
{
    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null for a node of no single repo, such as a decision that spans repos; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }

    /// <summary>True when the owner named it in the profile (a 'page:' evidence ref, pages spec §3.5 "Set by a person"); left out when false.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Owner { get; init; }
}
