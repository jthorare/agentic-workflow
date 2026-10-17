using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>One agent's entry in the profile's 'agents:' map (screens spec E-28, launcher L-11).</summary>
/// <param name="DailyBudget">The agent's daily budget, a decimal amount in USD; a target with no usage reader and a budget is refused (SND-LAUNCH-010).</param>
/// <param name="StepArgvHash">The 'argvHash' values an unattended Step may run for this agent when its entry isn't built in (SL8); one hash or a list in the file.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentLimits(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? DailyBudget = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(HashListJsonConverter))] IReadOnlyList<string>? StepArgvHash = null);
