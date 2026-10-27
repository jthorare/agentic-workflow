using System.Text.Json;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What the concept-scan jobs already did, read from the jobs view: the daily count the budget needs and the map versions that were looked at.</summary>
internal static class ConceptScanLog
{
    internal const string OverBudget = "over-budget";

    /// <summary>How many scans started since <paramref name="since"/> and ended, not counting those that stopped over budget or found no map (they used no model).</summary>
    internal static int Started(IEnumerable<JobRow> jobs, DateTimeOffset since) =>
        jobs.Count(j => j.State != JobState.Running && j.StartedAt >= since && Outcome(j) is not (OverBudget or "no-map"));

    /// <summary>Whether a scan that ended well already looked at <paramref name="version"/> (its outcome, even over budget, counts: the budget lifts for the next version, not for this one).</summary>
    internal static bool Handled(IEnumerable<JobRow> jobs, int version) =>
        jobs.Any(j => j.State == JobState.Succeeded && Version(j) == version);

    /// <summary>Whether a scan failed since <paramref name="since"/> (the service waits before it tries again).</summary>
    internal static bool FailedSince(IEnumerable<JobRow> jobs, DateTimeOffset since) =>
        jobs.Any(j => j.State == JobState.Failed && j.EndedAt >= since);

    private static string? Outcome(JobRow job) => Text(job, "outcome");

    private static int? Version(JobRow job) =>
        job.Result is { ValueKind: JsonValueKind.Object } result && result.TryGetProperty("version", out var value) && value.TryGetInt32(out var number) ? number : null;

    private static string? Text(JobRow job, string name) =>
        job.Result is { ValueKind: JsonValueKind.Object } result && result.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
