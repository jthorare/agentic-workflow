namespace Aw.Sindri.Core.Reviews;

/// <summary>The ci panel.</summary>
/// <param name="Runs">The check runs.</param>
/// <param name="Babysitter">The babysitter's outcome; null when no instance watches the PR.</param>
public sealed record CiData(IReadOnlyList<ReviewPageRun> Runs, ReviewPageBabysitter? Babysitter) : ReviewPanelData;
