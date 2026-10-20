using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>What the design changed in the acceptance list.</summary>
/// <param name="Added">Items the design added.</param>
/// <param name="Superseded">Items the design superseded.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopAcceptance(
    IReadOnlyList<StopItem> Added,
    IReadOnlyList<StopSuperseded> Superseded);
