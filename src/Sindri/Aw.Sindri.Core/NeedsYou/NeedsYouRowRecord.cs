using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>A row of the 'needs_you' view as stored: the wire row and the ledger sequence that last changed it.</summary>
/// <param name="Row">The row.</param>
/// <param name="Sequence">The ledger sequence that last changed it.</param>
/// <param name="Nested">On a root instance's 'park' row: the parked descendants below it (null when none). Not part of the wire row.</param>
public sealed record NeedsYouRowRecord(NeedsYouRow Row, long Sequence, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<NestedPark>? Nested = null);
