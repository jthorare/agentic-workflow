using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Features.RecordRun;

/// <summary>The record-run --build-failed: a candidate that doesn't build has no check to run, so mark it failed with its build log.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Candidate">The candidate.</param>
/// <param name="Log">The build log (non-empty).</param>
public sealed record RecordBuildFailedRequest(
    string StateDirectory,
    CandidateId Candidate,
    string Log);
