namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>A count and the median seconds behind it.</summary>
/// <param name="N">How many ended.</param>
/// <param name="MedianSeconds">Their median, null for none.</param>
public sealed record HumanTimeFigure(int N, long? MedianSeconds);
