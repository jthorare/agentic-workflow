namespace Aw.Meta.Core.Config;

/// <summary>Where one lane is assigned to run; every field is optional.</summary>
/// <param name="Agent">The catalog agent id (claude-code, codex, cursor). Null: the agent the model is listed under, else the lane's running agent.</param>
/// <param name="Model">The model name, as listed in routing.models. Null: routing.defaultModel.</param>
public sealed record LaneRoute(string? Agent = null, string? Model = null);
