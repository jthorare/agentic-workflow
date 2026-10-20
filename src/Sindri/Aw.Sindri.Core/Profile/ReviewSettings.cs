using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.review:' block (line spec §7.4, Appendix D).</summary>
/// <param name="Models">The lens model.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewModels? Models = null);
