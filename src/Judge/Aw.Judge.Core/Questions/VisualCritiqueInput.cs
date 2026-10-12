namespace Aw.Judge.Core.Questions;

/// <summary>visual-critique's input: a screenshot to judge and, when there is one, the base branch's.</summary>
/// <param name="AfterScreenshot">The screenshot's path.</param>
/// <param name="EvidenceDir">The evidence directory the screenshots are in.</param>
/// <param name="BaselineScreenshot">The base branch's screenshot of the same page, or null.</param>
public sealed record VisualCritiqueInput(string AfterScreenshot, string EvidenceDir, string? BaselineScreenshot = null);
