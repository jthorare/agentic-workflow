using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One state of the band.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Order">Order, as served.</param>
/// <param name="BranchOf">BranchOf, as served.</param>
/// <param name="Glyph">Glyph, as served.</param>
/// <param name="Badge">Badge, as served.</param>
/// <param name="Round">Round, as served.</param>
/// <param name="Loops">Loops, as served.</param>
/// <param name="Desk">Desk, as served.</param>
/// <param name="Child">Child, as served.</param>
/// <param name="Children">Children, as served.</param>
/// <param name="Outcome">Outcome, as served.</param>
/// <param name="Terminal">Terminal, as served.</param>
/// <param name="EnteredAt">When the latest visit to the state began; read as optional.</param>
/// <param name="LeftAt">When the latest visit ended; read as optional (none while the state is current).</param>
/// <param name="Block">The skill block behind the state (its id in GET /sindri/skills); null (omitted) for a state that runs no skill block.</param>
internal sealed record StateCell(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    StateGlyph Glyph,
    EngineOwnedBadge? Badge,
    Round? Round,
    IReadOnlyList<LoopPair> Loops,
    bool Desk,
    bool Child,
    Progress? Children,
    string? Outcome,
    bool Terminal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? EnteredAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? LeftAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Block = null);
