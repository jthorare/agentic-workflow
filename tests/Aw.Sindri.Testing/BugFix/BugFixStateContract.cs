using Aw.Sindri.Core.BugFix;
using Xunit;

namespace Aw.Sindri.Testing.BugFix;

/// <summary>
/// The contract every <see cref="IBugFixState"/> honours: the attempt cap and its order, a candidate for each attempt, the frozen check run on it, and the refusals a Step branches on.
/// A binding supplies a started bug fix and candidates that fix it or do not.
/// </summary>
public abstract class BugFixStateContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A bug fix that has reproduced its failing check and opened no attempt.</summary>
    protected abstract string StateDirectory { get; }

    /// <summary>A state directory with no bug fix in it.</summary>
    protected abstract string EmptyDirectory { get; }

    /// <summary>The check the baseline froze.</summary>
    protected abstract FrozenCheck Check { get; }

    /// <summary>The port over <see cref="StateDirectory"/>.</summary>
    /// <returns>The port.</returns>
    protected abstract IBugFixState Create();

    /// <summary>A new commit on a worktree, which fixes the bug or does not; a call makes a different commit each time.</summary>
    /// <param name="fixes">Whether the check passes on it.</param>
    /// <returns>Where the candidate is.</returns>
    protected abstract CandidateSource Commit(bool fixes);

    /// <summary>An attempt on a started bug fix opens as attempt 1 in the mode asked.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_first_attempt_is_number_one_in_the_mode_asked()
    {
        var started = await Create().StartAttempt(StateDirectory, AttemptMode.Split, Token);

        Expect.Equal(BugFixAnswerStatus.Ok, started.Status);
        Expect.Equal(new AttemptStarted(1, AttemptMode.Split), started.Value);
    }

    /// <summary>A directory with no bug fix refuses, with bugfix-state's code.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_directory_with_no_bug_fix_is_refused()
    {
        var started = await Create().StartAttempt(EmptyDirectory, AttemptMode.Whole, Token);

        Expect.Equal(BugFixAnswerStatus.Refused, started.Status);
        Expect.Equal("AW-BUGFIX-002", started.Refusal!.Code);
    }

    /// <summary>A candidate belongs to the open attempt and carries its commit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_candidate_is_recorded_under_the_open_attempt()
    {
        var port = Create();
        _ = await port.StartAttempt(StateDirectory, AttemptMode.Whole, Token);

        var recorded = await port.RecordCandidate(StateDirectory, Commit(fixes: true), Token);

        Expect.Equal(BugFixAnswerStatus.Ok, recorded.Status);
        Expect.Equal("c1", recorded.Value!.Candidate);
        Expect.Equal(1, recorded.Value.Attempt);
        Expect.True(recorded.Value.Commit.Length > 0);
        Expect.True(recorded.Value.ChangedFiles.Count > 0);
    }

    /// <summary>The frozen check passes on a candidate that fixes the bug.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_frozen_check_passes_on_a_fixing_candidate()
    {
        var run = await Recheck(fixes: true);

        Expect.Equal(BugFixAnswerStatus.Ok, run.Status);
        Expect.True(run.Value!.Passed);
        Expect.Equal("c1", run.Value.Candidate);
    }

    /// <summary>The frozen check fails on a candidate that does not fix it: an answer, not a refusal.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_frozen_check_fails_on_a_candidate_that_does_not_fix_it()
    {
        var run = await Recheck(fixes: false);

        Expect.Equal(BugFixAnswerStatus.Ok, run.Status);
        Expect.True(!run.Value!.Passed);
        Expect.True(run.Value.ExitCode != 0);
    }

    /// <summary>A candidate whose check run failed cannot be judged: the refusal says so.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_candidate_whose_check_failed_cannot_be_judged()
    {
        var port = Create();
        _ = await Recheck(port, fixes: false);

        var verdict = await port.Judge(StateDirectory, "c1", Token);

        Expect.Equal(BugFixAnswerStatus.Refused, verdict.Status);
        Expect.Equal(RefusalKind.NoPassingRun, verdict.Refusal!.Kind);
        Expect.Equal("AW-BUGFIX-015", verdict.Refusal.Code);
    }

    /// <summary>A second attempt waits for the first candidate's run.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_attempt_waits_until_the_candidate_has_a_run()
    {
        var port = Create();
        _ = await port.StartAttempt(StateDirectory, AttemptMode.Whole, Token);
        _ = await port.RecordCandidate(StateDirectory, Commit(fixes: false), Token);

        var second = await port.StartAttempt(StateDirectory, AttemptMode.Whole, Token);

        Expect.Equal(BugFixAnswerStatus.Refused, second.Status);
        Expect.Equal(RefusalKind.Other, second.Refusal!.Kind);
        Expect.Equal("AW-BUGFIX-005", second.Refusal.Code);   // still the fix phase until the candidate's run is recorded
    }

    /// <summary>Five attempts open; the sixth is refused with the cap's kind.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_sixth_attempt_is_refused_as_the_cap()
    {
        var port = Create();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var started = await port.StartAttempt(StateDirectory, AttemptMode.Whole, Token);
            Expect.Equal(attempt, started.Value!.Attempt);
            var source = Commit(fixes: false);
            _ = await port.RecordCandidate(StateDirectory, source, Token);
            _ = await port.Recheck(StateDirectory, "c" + attempt.ToString(System.Globalization.CultureInfo.InvariantCulture), source.Worktree, Check, Token);
        }

        var sixth = await port.StartAttempt(StateDirectory, AttemptMode.Whole, Token);

        Expect.Equal(BugFixAnswerStatus.Refused, sixth.Status);
        Expect.Equal(RefusalKind.AttemptCap, sixth.Refusal!.Kind);
    }

    private async Task<BugFixAnswer<CheckRun>> Recheck(bool fixes) => await Recheck(Create(), fixes);

    private async Task<BugFixAnswer<CheckRun>> Recheck(IBugFixState port, bool fixes)
    {
        _ = await port.StartAttempt(StateDirectory, AttemptMode.Whole, Token);
        var source = Commit(fixes);
        _ = await port.RecordCandidate(StateDirectory, source, Token);
        return await port.Recheck(StateDirectory, "c1", source.Worktree, Check, Token);
    }
}
