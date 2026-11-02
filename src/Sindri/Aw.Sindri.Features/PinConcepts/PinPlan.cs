using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>What a PinConcepts request does to the live profile, worked out and checked before anything is written.</summary>
/// <param name="Edits">The writes, sets first and then removals.</param>
/// <param name="Pins">Each pin touched, in request order.</param>
/// <param name="Dropped">The folder rules the removals drop, as 'id.key'.</param>
/// <param name="Features">The exact feature keys the sets name, which the map must hold.</param>
internal sealed record PinPlan(IReadOnlyList<ConceptEdit> Edits, IReadOnlyList<PinWritten> Pins, IReadOnlyList<string> Dropped, IReadOnlyList<string> Features);
