namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The list's error line (review page spec §6.3), shown above the last rows.</summary>
/// <param name="Text">"PR list is from 23:45: github failed: SND-INDEX-401".</param>
internal sealed record ReviewStaleView(string Text);
