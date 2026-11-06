namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>How fresh one source of the Reviews list is ("synced 14 min ago", "failing: SND-INDEX-401").</summary>
/// <param name="Source">The source ("github").</param>
/// <param name="State">"ok", "syncing" or "failing".</param>
/// <param name="Text">Sindri's words for the state.</param>
/// <param name="At">When the source last synced; null when it never did.</param>
internal sealed record ReviewFreshnessView(string Source, string State, string Text, DateTimeOffset? At);
