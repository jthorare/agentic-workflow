namespace Aw.Judge.Infrastructure.Providers;

/// <summary>What an image question's input holds for the CLIs: screenshots by path relative to the evidence directory.</summary>
/// <param name="AfterScreenshot">The screenshot to judge.</param>
/// <param name="EvidenceDir">The directory the screenshots are in.</param>
/// <param name="BaselineScreenshot">The screenshot to compare with, if any.</param>
/// <param name="More">Further images of a call whose input names more than two (any question's 'images'), by path relative to the evidence directory, or null.</param>
internal sealed record ImageEvidence(string AfterScreenshot, string EvidenceDir, string? BaselineScreenshot = null, IReadOnlyList<string>? More = null)
{
    /// <summary>The screenshots' absolute paths: the after one, then the baseline when there is one, then the rest.</summary>
    public IReadOnlyList<string> Files =>
        [Path.GetFullPath(AfterScreenshot, EvidenceDir), .. BaselineScreenshot is null ? [] : new[] { Path.GetFullPath(BaselineScreenshot, EvidenceDir) }, .. (More ?? []).Select(file => Path.GetFullPath(file, EvidenceDir))];
}
