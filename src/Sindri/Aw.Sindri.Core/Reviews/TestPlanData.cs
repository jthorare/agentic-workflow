namespace Aw.Sindri.Core.Reviews;

/// <summary>The testPlan panel.</summary>
/// <param name="Rows">The TP rows.</param>
public sealed record TestPlanData(IReadOnlyList<ReviewPageTestRow> Rows) : ReviewPanelData;
