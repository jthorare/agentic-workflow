namespace Aw.Sindri.Core.Forge;

/// <summary>The files a pull request changes.</summary>
/// <param name="Files">The files, in the forge's order.</param>
/// <param name="CapReached">True when the pull request has more files than the forge lists (GitHub stops at 3,000), so the page can say so.</param>
public sealed record PullFiles(IReadOnlyList<PullFile> Files, bool CapReached);
