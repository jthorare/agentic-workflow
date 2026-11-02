using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.agent:' block (engine spec §6.5, launcher L-14; model-pick spec §8): where a Step whose runtime is the agent runs, and which model it runs on.</summary>
/// <param name="Target">The agent a Step launches; an agent id checked by pattern only (the catalog is per box). Absent: no agent runtime, so a deskCapable Step runs at the desk and any other parks (SND-WF-010).</param>
/// <param name="Models">Agent id to its models, cheapest first, and the default: overrides discovery (model-pick spec §3).</param>
/// <param name="Pin">An agent id, or 'agent/stateId', to the model every step of it runs on; no pick is asked.</param>
/// <param name="Cap">Agent id to the model no pick goes above.</param>
/// <param name="Pick">'on' or 'off'; absent is off until the owner has approved a profile whose summary showed the notice (model-pick spec §8, "Consent").</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkflowAgentSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentId? Target = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, AgentModels>? Models = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Pin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Cap = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pick = null)
{
    /// <summary>The pick is asked for (once consent is recorded).</summary>
    public const string On = "on";

    /// <summary>No pick is made and nothing is sent to Jev: the privacy switch and the old behaviour.</summary>
    public const string Off = "off";

    /// <summary>The line the approval shows when the pick is on: what is sent to Jev (model-pick spec 8).</summary>
    public const string Notice = "Model choice is on: each agent step's brief and input files (up to 100,000 characters, secrets and denied paths removed), and for the concept scan its map summary, are sent to Jev to pick a model.";

    /// <summary>True when the profile asks for the pick (<see cref="Pick"/> is 'on'); the one place that reads the word.</summary>
    [JsonIgnore]
    public bool PickIsOn => string.Equals(Pick, On, StringComparison.Ordinal);
}
