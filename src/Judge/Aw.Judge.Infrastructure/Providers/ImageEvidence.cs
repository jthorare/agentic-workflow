namespace Aw.Judge.Infrastructure.Providers;

/// <summary>What an image question's input holds for the CLIs: screenshots by path relative to the evidence directory.</summary>
/// <param name="AfterScreenshot">The screenshot to judge.</param>
/// <param name="EvidenceDir">The directory the screenshots are in.</param>
/// <param name="BaselineScreenshot">The screenshot to compare with, if any.</param>
internal sealed record ImageEvidence(string AfterScreenshot, string EvidenceDir, string? BaselineScreenshot = null)
{
    /// <summary>The screenshots' absolute paths: the after one, then the baseline when there is one.</summary>
    public IReadOnlyList<string> Files =>
        [Path.GetFullPath(AfterScreenshot, EvidenceDir), .. BaselineScreenshot is null ? [] : new[] { Path.GetFullPath(BaselineScreenshot, EvidenceDir) }];
}
