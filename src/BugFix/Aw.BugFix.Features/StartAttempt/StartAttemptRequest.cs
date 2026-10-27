using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.StartAttempt;

/// <summary>The start-attempt: begin the next fix attempt in a mode.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Mode">The attempt's mode.</param>
/// <param name="AbandonJudge">Whether the user chose to leave behind a candidate whose judge question was never answered.</param>
/// <param name="AbandonUnrun">Whether the user chose to leave behind a candidate that has no recorded run (record-run refused it), so the attempt can't go on.</param>
public sealed record StartAttemptRequest(
    string StateDirectory,
    FixMode Mode,
    bool AbandonJudge = false,
    bool AbandonUnrun = false);
