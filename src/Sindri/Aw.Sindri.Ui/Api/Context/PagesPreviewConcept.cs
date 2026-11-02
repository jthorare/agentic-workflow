namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One concept written after '->'.</summary>
/// <param name="Written">The text as pasted (or the id, for a concept the matched action already had).</param>
/// <param name="Id">The concept id it resolved to; null when it names no concept.</param>
internal sealed record PagesPreviewConcept(string Written, string? Id);
