using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.ship:' block (line spec §7.5, Appendix D).</summary>
/// <param name="Draft">Whether the line opens its pull requests as drafts (default false).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShipSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Draft = null);
