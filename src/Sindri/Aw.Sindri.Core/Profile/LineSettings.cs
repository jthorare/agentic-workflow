using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.line:' block (line spec §8, Appendix D).</summary>
/// <param name="Parallel">How many phases run at once (default 2); it cannot be above the box's heavy-slot cap.</param>
/// <param name="MaxOpenPrs">How many pull requests the line keeps open (default 4).</param>
/// <param name="MaxStack">How deep a stack of phases may go (default 4; PLN-STACK-001).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Parallel = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxOpenPrs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxStack = null);
