using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Testing;

namespace Aw.BugFix.Tests.Slices;

public sealed class FixTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Start_attempt_opens_attempt_one_from_reproduce()
    {
        await _w.Reproduced();

        var result = World.Ok(await _w.Start());

        Assert.Equal((Phase.Fix, 1, FixMode.A), (result.Phase, result.Attempt, result.Mode));
        Assert.Equal("start-attempt A", _w.Now.History[^1].Command);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_attempt_refuses_before_the_bug_is_reproduced_and_mode_B_first()
    {
        await _w.Investigated();
        _ = World.Refused(await _w.Start(), BugFixErrors.WrongPhase);

        await ReproduceOnly();
        var error = World.Refused(await _w.Start(FixMode.B), BugFixErrors.AttemptRefused);
        Assert.Contains("escalation after a failed attempt", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_attempt_refuses_while_a_candidate_is_unevaluated_and_after_the_cap()
    {
        var c1 = await _w.Candidate();
        _ = World.Refused(await _w.Start(), BugFixErrors.WrongPhase);

        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));
        for (var attempt = 2; attempt <= BugFixState.MaxAttempts; attempt++)
        {
            _ = World.Ok(await _w.Start());
            var worktree = string.Create(CultureInfo.InvariantCulture, $"/wt/a{attempt}");
            var branch = string.Create(CultureInfo.InvariantCulture, $"bugfix/eng-1-a{attempt}");
            _ = _w.Fix(branch, worktree, new(StringComparer.Ordinal) { ["src/app.ts"] = worktree });
            var c = World.Ok(await _w.Record(branch, worktree)).Candidate;
            _ = World.Ok(await RunAt(c, worktree, exitCode: 1));
        }

        var error = World.Refused(await _w.Start(), BugFixErrors.AttemptRefused);
        Assert.Contains("attempt cap (5) reached", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mode_B_needs_two_open_hypotheses()
    {
        _w.Repos.Write(World.HandoffFile, A.Handoff(results: ["confirmed", "ruled-out"]));
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));

        var error = World.Refused(await _w.Start(FixMode.B), BugFixErrors.AttemptRefused);

        Assert.Contains("at least 2 hypotheses not ruled out", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_candidate_records_the_commit_and_the_files_it_changed()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        var commit = _w.Fix();

        var result = World.Ok(await _w.Record());

        Assert.Equal((CandidateId.From("c1"), commit), (result.Candidate, result.Commit));
        Assert.Equal(["src/app.ts"], result.ChangedFiles);
        var candidate = Assert.Single(_w.Now.Candidates);
        Assert.Equal((1, FixMode.A, World.Worktree), (candidate.Attempt, candidate.Mode, candidate.Worktree));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("tests/bug.test.sh")]
    [InlineData("tests/helpers.sh")]
    [InlineData("src/app.test.ts")]
    [InlineData("vitest.config.ts")]
    [InlineData("App/LoginTests.swift")]
    public async Task Record_candidate_refuses_a_fix_that_touches_tests_or_their_config_unless_approved(string file)
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "fixed\n", [file] = "weakened\n" });

        var error = World.Refused(await _w.Record(), BugFixErrors.CandidateRefused);

        Assert.Contains(file, error.Message, StringComparison.Ordinal);
        Assert.Contains(file, World.Ok(await _w.Record(allowTestChanges: true)).ChangedFiles, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("package.json")]
    [InlineData("Directory.Build.props")]
    [InlineData("go.mod")]
    [InlineData("tsconfig.json")]
    [InlineData("Makefile")]
    public async Task Record_candidate_needs_no_flag_for_build_files(string file)
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "fixed\n", [file] = "dep bump\n" });

        Assert.Contains(file, World.Ok(await _w.Record()).ChangedFiles, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_test_change_records_its_reason_in_the_history()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "fixed\n", ["tests/helpers.sh"] = "new fixture shape\n" });

        _ = World.Ok(await _w.Record(allowTestChanges: true, reason: "the fixture encoded the old API"));

        Assert.Contains("the fixture encoded the old API", _w.Now.History[^1].Command, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_test_change_names_the_flag_and_asks_no_human()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "fixed\n", ["tests/helpers.sh"] = "x\n" });

        var message = World.Refused(await _w.Record(), BugFixErrors.CandidateRefused).Message;

        Assert.Contains("--allow-test-changes", message, StringComparison.Ordinal);
        Assert.DoesNotContain("ask the user", message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Start_attempt_refuses_a_fix_for_an_unconfirmed_cause_and_mode_B_takes_it()
    {
        _w.Repos.Write(World.HandoffFile, A.Handoff(results: ["untested", "untested"]));
        await _w.Investigated();
        await ReproduceOnly();

        Assert.Contains("mode B", World.Refused(await _w.Start(), BugFixErrors.AttemptRefused).Message, StringComparison.Ordinal);
        Assert.Contains("mode B", World.Ok(await _w.Run<Features.Resume.ResumeRequest, Features.Resume.ResumeResponse>(new(World.State))).Next, StringComparison.Ordinal);
        Assert.Equal(FixMode.B, World.Ok(await _w.Start(FixMode.B)).Mode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_candidate_lets_through_names_that_only_look_like_tests()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/Latest.kt"] = "x\n", ["src/Contest.cs"] = "y\n" });

        _ = World.Ok(await _w.Record());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_candidate_refuses_what_is_not_a_clean_change_on_the_baseline()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _w.Repos.AddWorktree(World.Worktree, "bugfix/eng-1-a1", _w.Base);
        Assert.Contains("no commits since the baseline", Refused(await _w.Record()), StringComparison.Ordinal);

        _w.Repos.Write($"{World.Worktree}/src/app.ts", "fixed\n");
        Assert.Contains("uncommitted changes", World.Refused(await _w.Record(), BugFixErrors.DirtyTree).Message, StringComparison.Ordinal);

        _ = _w.Repos.Commit(World.Worktree);
        Assert.Contains("is on branch bugfix/eng-1-a1, not other", Refused(await _w.Record(branch: "other")), StringComparison.Ordinal);
        _ = World.Ok(await _w.Record());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_candidate_refuses_a_commit_that_does_not_build_on_the_baseline_or_changes_nothing()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        var unrelated = _w.Repos.Init("/elsewhere", new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "b" });
        _w.Repos.AddWorktree(World.Worktree, "bugfix/eng-1-a1", unrelated);
        _w.Repos.Write($"{World.Worktree}/a", "c");
        _ = _w.Repos.Commit(World.Worktree);

        Assert.Contains("does not build on the baseline", Refused(await _w.Record()), StringComparison.Ordinal);

        _ = _w.Fix("bugfix/eng-1-a1b", "/wt/same", new(StringComparer.Ordinal) { ["src/app.ts"] = "drop cookie\n" });
        Assert.Contains("identical to the baseline", Refused(await _w.Record("bugfix/eng-1-a1b", "/wt/same")), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mode_B_records_two_candidates_on_different_open_hypotheses()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));
        _ = World.Ok(await _w.Start(FixMode.B));
        _ = _w.Fix("h1", "/wt/h1", new(StringComparer.Ordinal) { ["src/app.ts"] = "h1\n" });
        _ = _w.Fix("h2", "/wt/h2", new(StringComparer.Ordinal) { ["src/b.ts"] = "h2\n" });
        _ = _w.Fix("h3", "/wt/h3", new(StringComparer.Ordinal) { ["src/c.ts"] = "h3\n" });

        Assert.Contains("must name their hypothesis", Refused(await _w.Record("h1", "/wt/h1")), StringComparison.Ordinal);
        Assert.Contains("has no hypothesis 7", Refused(await _w.Record("h1", "/wt/h1", hypothesis: 7)), StringComparison.Ordinal);
        _ = World.Ok(await _w.Record("h1", "/wt/h1", hypothesis: 1));
        Assert.Contains("already pursued by c2", Refused(await _w.Record("h2", "/wt/h2", hypothesis: 1)), StringComparison.Ordinal);
        _ = World.Ok(await _w.Record("h2", "/wt/h2", hypothesis: 2));
        Assert.Contains("allows 2 candidate(s) per attempt", Refused(await _w.Record("h3", "/wt/h3", hypothesis: 2)), StringComparison.Ordinal);
        _ = World.Ok(await RunAt(CandidateId.From("c2"), "/wt/h1", exitCode: 1));
        Assert.Contains("c3 has not been evaluated yet", World.Refused(await _w.Start(), BugFixErrors.AttemptRefused).Message, StringComparison.Ordinal);
    }

    private static string Refused<T>(Kernel.Result<T> result) => World.Refused(result, BugFixErrors.CandidateRefused).Message;

    private async Task ReproduceOnly()
    {
        var run = World.Ok(await _w.TestRun());
        _ = World.Ok(await _w.Run<Features.AdvanceReproduce.AdvanceReproduceRequest, Features.AdvanceReproduce.AdvanceReproduceResponse>(
            new Features.AdvanceReproduce.AdvanceReproduceRequest(World.State, run.Evidence, World.Check, World.Repo)));
    }

    private async Task<Kernel.Result<Features.RecordRun.RecordRunResponse>> RunAt(CandidateId candidate, string worktree, int exitCode)
    {
        _ = _w.Runner.Exits("bash", exitCode);
        var run = World.Ok(await _w.TestRun(worktree));
        return await _w.Run<Features.RecordRun.RecordRunRequest, Features.RecordRun.RecordRunResponse>(new Features.RecordRun.RecordRunRequest(World.State, candidate, run.Evidence));
    }
}
