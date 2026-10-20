using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One recorded run.</summary>
/// <param name="Model">The model.</param>
/// <param name="Vendor">The vendor, when recorded.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopModel(
    string Model,
    string? Vendor);
