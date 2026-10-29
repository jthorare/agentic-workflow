using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The words of 'context:index-sync': each record source's last sync, the same data as 'sindri index status'.</summary>
internal static class IndexSyncRules
{
    private const string Fix = "sindri index sync, then sindri index status";

    /// <summary>Warn for a source not synced yet or whose last sync failed; ok otherwise, the time cap (SND-CONTEXT-059) included, since the sync just continues next tick.</summary>
    /// <param name="rows">The feeds' progress.</param>
    /// <returns>The row.</returns>
    internal static DoctorCheck Judge(IReadOnlyList<SourceProgress> rows)
    {
        if (rows.Count == 0)
        {
            return new DoctorCheck(IndexDoctorChecks.SyncName, CheckState.Ok, "no record source feeds a repo yet", Fix: null);
        }

        var lines = "record index: " + string.Join("; ", rows.Select(Line));
        return rows.Any(IsProblem)
            ? new DoctorCheck(IndexDoctorChecks.SyncName, CheckState.Warn, lines, Fix)
            : new DoctorCheck(IndexDoctorChecks.SyncName, CheckState.Ok, lines, Fix: null);
    }

    private static bool IsProblem(SourceProgress p) => p.LastSync is null || Failure(p) is not null;

    private static string? Failure(SourceProgress p) => p.LastCode is { } code && !string.Equals(code, ContextErrors.SyncCapped.Code, StringComparison.Ordinal) ? code : null;

    private static string Line(SourceProgress p) => Failure(p) is { } code
        ? $"{p.Source.Value} last sync failed: {code}"
        : p.ReReading is { } stored
            ? string.Create(CultureInfo.InvariantCulture, $"{p.Source.Value} re-reading for project links, {stored:N0} issues stored")
            : p.LastSync is not { } at
                ? $"{p.Source.Value} not synced yet"
                : $"{p.Source.Value} synced {at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}Z";
}
