namespace Aw.Evidence.Core.Desktop;

/// <summary>One screen at one window size: the pixel verdict and where its images are.</summary>
/// <param name="Name">The screen.</param>
/// <param name="Size">The window size, 'WxH'.</param>
/// <param name="DiffPercent">The share of pixels that differ, in percent (two decimals); 0 when the frames could not be compared.</param>
/// <param name="Threshold">The limit it was judged by, in percent.</param>
/// <param name="Passed">True when the frames compared and the difference is within the limit.</param>
/// <param name="Problem">Why the screen failed beyond the difference (a capture of another size, an unreadable image); null otherwise.</param>
/// <param name="Files">The screen's images (absolute paths).</param>
/// <param name="Accepted">The differences the manifest lists as accepted; the review step skips them.</param>
/// <param name="Masked">How many mask regions were painted over.</param>
public sealed record DesktopScreenResult(
    string Name,
    string Size,
    double DiffPercent,
    double Threshold,
    bool Passed,
    string? Problem,
    DesktopFiles Files,
    IReadOnlyList<string> Accepted,
    int Masked);
