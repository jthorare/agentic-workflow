using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.steps:' block (line spec LN-WF7, Appendix D).</summary>
/// <param name="LongMax">The longest a block Step may declare for its MaxDuration (default 2h): a duration.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StepsSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LongMax = null);
