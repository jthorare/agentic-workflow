
namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>One evidence check as an event records it (spec §5.6).</summary>
/// <param name="Kind">The evidence kind.</param>
/// <param name="At">Base or head.</param>
/// <param name="Expect">What the recipe expected.</param>
/// <param name="Held">Whether the evidence met the expectation.</param>
/// <param name="Reasons">The provider's reasons.</param>
public sealed record EvidenceRecord(string Kind, EvidencePoint At, EvidenceExpectation Expect, bool Held, IReadOnlyList<string> Reasons);
