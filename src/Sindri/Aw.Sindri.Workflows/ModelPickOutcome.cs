using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The pick's result: the model to pass to the agent (null for its own default), or why no model is allowed.</summary>
/// <param name="Model">The model id; null runs the agent's default.</param>
/// <param name="Refusal">SND-MODEL-003 when no model is left; else null.</param>
/// <param name="CanEscalate">True when a stronger model is still allowed inside the pin and cap, the Step names no model of its own and reads no workspace, and this was not already the retry (spec 7.1).</param>
internal sealed record ModelPickOutcome(string? Model, Error? Refusal = null, bool CanEscalate = false);
