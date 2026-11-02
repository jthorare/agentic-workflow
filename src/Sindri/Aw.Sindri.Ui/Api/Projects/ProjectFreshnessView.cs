namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>How fresh one source of the Projects page is ("synced 14 min ago", "still syncing", "needs attention (SND-INDEX-401)").</summary>
/// <param name="Source">The source ("linear-projects", "linear", "github", "context").</param>
/// <param name="State">"ok", "syncing" or "failing".</param>
/// <param name="Text">Sindri's words for the state.</param>
/// <param name="At">When the source last synced; null when it never did.</param>
internal sealed record ProjectFreshnessView(string Source, string State, string Text, DateTimeOffset? At);
