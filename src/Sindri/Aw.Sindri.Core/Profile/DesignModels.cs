using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The models of the design station (line spec §4.3 DSN-LENS-002).</summary>
/// <param name="Writer">The agent that writes the design.</param>
/// <param name="Lens">The agent that runs the design lenses.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesignModels(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Writer = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Lens = null);
