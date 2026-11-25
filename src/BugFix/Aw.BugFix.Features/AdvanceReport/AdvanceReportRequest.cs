using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Features.AdvanceReport;

/// <summary>The advance report: finish as resolved by a candidate, or unresolved.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Candidate">The resolving candidate.</param>
/// <param name="Unresolved">Whether to finish unresolved.</param>
/// <param name="SweepFile">The sweep file (the "Where else we checked" table and the Prevention decision) a resolved report needs.</param>
public sealed record AdvanceReportRequest(
    string StateDirectory,
    CandidateId? Candidate,
    bool Unresolved,
    string? SweepFile = null);
