namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a state's 'model' parameter asks for: a model id written in the recipe, or a profile key the engine can't resolve for an agent Step yet.</summary>
/// <param name="Literal">The model id the recipe wrote; null when it wrote none.</param>
/// <param name="Profile">The profile key of a '{ profile: … }' value; null when the value is not one.</param>
internal sealed record ModelAsk(string? Literal, string? Profile);
