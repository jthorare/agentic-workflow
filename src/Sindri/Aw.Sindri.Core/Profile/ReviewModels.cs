using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The models of the review station.</summary>
/// <param name="Lens">The agent that runs the review lenses.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewModels(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Lens = null);
