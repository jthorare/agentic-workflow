using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>What a recipe state says about the model its agent Step runs on (model-pick spec 8): today only a floor. Written into the hashed form only when the state has one.</summary>
/// <param name="Floor">The lowest tier the state accepts: 'cheap', 'standard' or 'strong' (a tier, not a model id, so a recipe never names a model that disappears); null for no floor. SND-MODEL-002 checks it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateModel([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Floor = null);
