namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The last choices used, read from the small file under the Sindri home (A7 owns the file).</summary>
/// <param name="Repo">The last repo; null for the first profile repo.</param>
/// <param name="Agent">The last agent's wire id; null for Claude Code.</param>
internal sealed record NewAgentDefaults(string? Repo, string? Agent);
