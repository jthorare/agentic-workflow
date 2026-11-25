namespace Aw.Meta.Features.ReportDefect;

/// <summary>The ReportDefect request: a defect in the toolkit itself.</summary>
/// <param name="Code">The error code (AW-META-010, SND-SCRUB-002).</param>
/// <param name="Title">A short title, one line.</param>
/// <param name="What">What happened.</param>
/// <param name="ErrorLine">The exact error line, one line.</param>
/// <param name="Path">The code or doc path the defect is about; null for none.</param>
public sealed record ReportDefectRequest(string Code, string Title, string What, string ErrorLine, string? Path = null);
