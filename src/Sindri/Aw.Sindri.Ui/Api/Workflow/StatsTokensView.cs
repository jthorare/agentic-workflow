namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The tokens a recipe's runs used (GET /workflow/recipes/{id}/stats).</summary>
/// <param name="P50">The typical run; null when none were counted.</param>
/// <param name="P90">The heavy run (nine in ten used less); null when none were counted.</param>
internal sealed record StatsTokensView(long? P50, long? P90);
