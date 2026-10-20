using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>The owner's single read of the design and the plan at stop 2 (line spec §6.4, LN-API2), ordered so behaviour comes before ticks. Live before the evidence step: a section whose data is not there yet is empty and named in <paramref name="NotYet"/>. Hidden scenarios are shown by count only.</summary>
/// <param name="NotYet">The sections with no data yet, by their member names here (for example "graph", "lensRounds").</param>
/// <param name="Accepted">Lens findings left that the owner accepted as known risks, shown first.</param>
/// <param name="Models">The writer and lens models, and the warnings about them (DSN-LENS-002).</param>
/// <param name="Decisions">The open decisions with their options, trade-offs and recommendation.</param>
/// <param name="Acceptance">The acceptance items the design added and the ones it superseded.</param>
/// <param name="Escapes">What the design adds beyond the scope packet (LC-7).</param>
/// <param name="Graph">The plan graph: stacks, waves, phases, the critical path and the PR count.</param>
/// <param name="Trace">The trace table: item to phase to signals.</param>
/// <param name="Coverage">The coverage table: surface to sections, or excluded with a reason.</param>
/// <param name="Spec">The spec text and the assumes markers linked to their decisions.</param>
/// <param name="Contracts">The cross-spec contract edits and their checks.</param>
/// <param name="LensRounds">The lens-round timeline.</param>
/// <param name="FreezeRefused">The banner when freeze reported a mismatch after an approve; null otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesignStopView(
    IReadOnlyList<string> NotYet,
    IReadOnlyList<StopAccepted> Accepted,
    StopModels Models,
    IReadOnlyList<StopDecision> Decisions,
    StopAcceptance Acceptance,
    IReadOnlyList<StopEscape> Escapes,
    StopGraph Graph,
    IReadOnlyList<StopTraceRow> Trace,
    IReadOnlyList<StopCoverageRow> Coverage,
    StopSpec Spec,
    IReadOnlyList<StopContract> Contracts,
    IReadOnlyList<StopLensRound> LensRounds,
    StopFreezeRefused? FreezeRefused);
