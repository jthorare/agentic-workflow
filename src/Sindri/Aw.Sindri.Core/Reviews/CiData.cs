namespace Aw.Sindri.Core.Reviews;

/// <summary>The ci panel.</summary>
/// <param name="Runs">The check runs.</param>
public sealed record CiData(IReadOnlyList<ReviewPageRun> Runs) : ReviewPanelData;
