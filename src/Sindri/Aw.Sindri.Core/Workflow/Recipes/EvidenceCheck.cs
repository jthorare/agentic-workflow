using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>Evidence a transition requires (spec §3.5): '{ kind, at, expect }'.</summary>
/// <param name="Kind">The evidence kind, as an 'evidence.…' provider declares it.</param>
/// <param name="At">Collected at base or head.</param>
/// <param name="Expect">Whether it must pass or fail.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvidenceCheck(string Kind, EvidencePoint At, EvidenceExpectation Expect);
