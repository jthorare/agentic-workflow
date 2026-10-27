using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>A file with its links to scope nodes.</summary>
/// <param name="Path">The path in the repo.</param>
/// <param name="Implements">The scope nodes it implements.</param>
/// <param name="Tests">The scope nodes it tests.</param>
/// <param name="Provenance">Of its best link.</param>
/// <param name="Confidence">Of its best link.</param>
/// <param name="Confirmed">True when some link of it is confirmed.</param>
/// <param name="Suggested">True when every link of it is an unconfirmed (model) suggestion.</param>
/// <param name="LinkId">The best unconfirmed link, to accept or reject it; null unless suggested.</param>
/// <param name="Label">The path, as the caller reads it, so the item can be drawn alone.</param>
/// <param name="Kind">Its kind (always code).</param>
/// <param name="Trust">Whether its label may be read as instructions (the best link's).</param>
public sealed record LensFile(
    string Path,
    IReadOnlyList<NodeKey> Implements,
    IReadOnlyList<NodeKey> Tests,
    Provenance Provenance,
    double Confidence,
    bool Confirmed,
    bool Suggested,
    LinkId? LinkId,
    string Label,
    NodeKind Kind,
    Results.Trust Trust)
{
    /// <summary>Schema 2 only: every anchor that reached it, in request order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Anchors { get; init; }

    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }

    /// <summary>Schema 2 only: the fewest edges or cascade levels from any anchor that reached it (0 for an anchor's own scope).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Depth { get; init; }

    /// <summary>True when the file's repo demotes it (index.demote): it stays readable and ranks last, but is never a scope surface.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Demoted { get; init; }

    /// <summary>The name of the role that demotes it (<see cref="DemotedCode.Role"/>: 'test', 'fixture', 'script' or whatever the repo's index.roles call it); left out when it is not demoted.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }
}
