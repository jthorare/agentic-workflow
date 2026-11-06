namespace Aw.Forge.Core.Pulls;

/// <summary>The files a pull request changes.</summary>
/// <param name="Files">The files, in the forge's order.</param>
/// <param name="CapReached">True when the pull request has more files than <see cref="ForgeLimits.PullFileCap"/> and the list stops there.</param>
public sealed record PullFiles(IReadOnlyList<PullFile> Files, bool CapReached);
