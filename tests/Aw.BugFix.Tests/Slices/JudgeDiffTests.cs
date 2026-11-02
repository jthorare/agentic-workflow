using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Testing;

namespace Aw.BugFix.Tests.Slices;

/// <summary>The judge sees the fix a "Part of" PR already merged to main, and the frozen check's own source (#1189).</summary>
public sealed class JudgeDiffTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task The_diff_holds_main_commits_that_say_part_of_or_fixes_this_issue()
    {
        _w.Repos.Write(World.TicketFile, A.TicketJson(source: "github", id: "#1189"));
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        MainCommit("src/session.ts", "keep session\n", "fix: keep the session\n\nPart of #1189");
        MainCommit("src/redirect.ts", "keep redirect\n", "fix: keep the redirect\n\nFixes #1189");

        var asked = await Judged();

        Assert.Contains("src/session.ts", asked.Diff, StringComparison.Ordinal);
        Assert.Contains("src/redirect.ts", asked.Diff, StringComparison.Ordinal);
        Assert.Contains("src/app.ts", asked.Diff, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_diff_leaves_out_main_commits_of_other_issues_and_unrelated_ones()
    {
        _w.Repos.Write(World.TicketFile, A.TicketJson(source: "github", id: "#1189"));
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        MainCommit("src/prefix.ts", "x\n", "fix: another\n\nPart of #118");
        MainCommit("src/other.ts", "y\n", "fix: another\n\nPart of #1190");
        MainCommit("src/plain.ts", "z\n", "chore: unrelated, mentions #1189 only");

        var asked = await Judged();

        Assert.DoesNotContain("src/prefix.ts", asked.Diff, StringComparison.Ordinal);
        Assert.DoesNotContain("src/other.ts", asked.Diff, StringComparison.Ordinal);
        Assert.DoesNotContain("src/plain.ts", asked.Diff, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ticket_that_is_not_a_github_issue_gets_no_main_commits()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        MainCommit("src/session.ts", "keep session\n", "fix: keep the session\n\nPart of #1189");

        var asked = await Judged();

        Assert.DoesNotContain("src/session.ts", asked.Diff, StringComparison.Ordinal);
        Assert.Contains("keep cookie", asked.Diff, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_input_carries_the_frozen_checks_own_source_at_the_candidates_commit()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = World.Ok(await _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, c1)));

        var asked = Assert.Single(_w.Judge.Asked);

        Assert.Equal("exit 1\n", asked.CheckSource);
    }

    private void MainCommit(string path, string text, string message)
    {
        _w.Repos.Write($"{World.Repo}/{path}", text);
        _ = _w.Repos.Commit(World.Repo, message);
    }

    private async Task<ResolutionCheckInput> Judged()
    {
        _ = _w.Fix();
        var c1 = World.Ok(await _w.Record()).Candidate;
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = World.Ok(await _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, c1)));
        return Assert.Single(_w.Judge.Asked);
    }
}
