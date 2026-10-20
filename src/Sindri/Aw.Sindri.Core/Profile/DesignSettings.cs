using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.design:' block (line spec §4, Appendix D).</summary>
/// <param name="Models">The writer and lens models.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesignSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DesignModels? Models = null);
