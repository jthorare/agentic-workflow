using System.Globalization;

namespace Aw.Context.Core.ConceptScan;

/// <summary>The daily limit on scans: the decision is pure, the count comes from the job log.</summary>
public static class ConceptScanBudget
{
    /// <summary>Whether another scan may start.</summary>
    /// <param name="startedInWindow">How many scans started in the last <see cref="ConceptScanLimits.Window"/>, those that ended over budget not counted.</param>
    /// <returns>Null when it may, else the reason it may not.</returns>
    public static string? Refusal(int startedInWindow) =>
        startedInWindow < ConceptScanLimits.MaxScansPerDay
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"over budget: {startedInWindow} concept scans started in the last 24 hours (the limit is {ConceptScanLimits.MaxScansPerDay})");
}
