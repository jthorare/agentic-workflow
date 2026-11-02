using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>One agent's entry under 'workflow.agent.models' (model-pick spec §8): the models it can use, cheapest first, and the one a step without a pick runs on.</summary>
/// <param name="List">The model ids, cheapest first. Overrides discovery for that agent (spec §3).</param>
/// <param name="Default">The model a step runs on when nothing picks one; one of <paramref name="List"/> (SND-MODEL-001).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AgentModels(IReadOnlyList<string> List, string Default);
