namespace Aw.Sindri.Core.Launch;

/// <summary>The drafter's launch as the challenger's pick sees it (model-pick spec §4.1 item 3).</summary>
/// <param name="SameAgent">True when the challenger runs on the drafter's agent; a different agent satisfies the rule by itself.</param>
/// <param name="Model">The model the drafter ran on ('launch.started' Model); null when it was not recorded.</param>
public sealed record DrafterModel(bool SameAgent, string? Model);
