namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The Reviews tab's login prompt (review page spec §6.3, SND-REVIEW-007): one field and Save.</summary>
/// <param name="Code">SND-REVIEW-007.</param>
/// <param name="Text">The prompt's heading ("Set your GitHub login").</param>
/// <param name="Suggested">The forge credential's own login to pre-fill; null when Sindri can't tell.</param>
/// <param name="Connection">The GitHub connection's name, or null when there is none.</param>
/// <param name="Reason">"unreachable", "signIn", "unsupportedHost" or "none"; null from an older server.</param>
internal sealed record ReviewLoginPromptView(string Code, string Text, string? Suggested, string? Connection = null, string? Reason = null);
