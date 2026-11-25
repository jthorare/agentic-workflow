namespace Aw.Meta.Features.ReportDefect;

/// <summary>The ReportDefect response.</summary>
/// <param name="Outcome">"filed", "commented" or "cap-hit".</param>
/// <param name="Url">The issue filed or commented on; null when the cap was hit.</param>
/// <param name="LogLine">The line appended to the running log.</param>
/// <param name="Skipped">Defects skipped today because the cap was hit (counting this one on a cap hit).</param>
public sealed record ReportDefectResponse(string Outcome, string? Url, string LogLine, int Skipped = 0);
