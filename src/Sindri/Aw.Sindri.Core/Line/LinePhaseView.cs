using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One phase of the line, as the board and its linked instance show it.</summary>
/// <param name="Id">The phase's id as the plan names it.</param>
/// <param name="Attempt">The attempt that started last; 0 before the first.</param>
/// <param name="Address">The linked instance '{line}/phase.{id}.{attempt}'; null before the phase starts.</param>
/// <param name="State">'waiting', 'running', 'shipped', 'merged', 'failed' or 'skipped'.</param>
/// <param name="ParkReason">Why the phase is parked; null when it is not.</param>
/// <param name="Pr">The phase's pull request link; null before it ships.</param>
/// <param name="Base">What the phase builds on: the head branch of the stack parent, or 'default-branch'; null before the phase starts.</param>
/// <param name="Worktree">The phase's worktree path (LN-WF2), to open in Warp; null before its seed.</param>
/// <param name="Now">What the phase's instance does now (its next sentence); empty before it starts.</param>
/// <param name="Tokens">The tokens the phase's instance has spent.</param>
/// <param name="WallMinutes">Minutes from the phase's start to now, or to its end.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinePhaseView(
    LinePhaseId Id,
    int Attempt,
    string? Address,
    string State,
    string? ParkReason,
    string? Pr,
    string? Base,
    string? Worktree,
    string Now,
    int Tokens,
    int WallMinutes);
